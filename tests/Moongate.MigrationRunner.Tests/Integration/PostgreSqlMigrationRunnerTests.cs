using Moongate.MigrationRunner.Services;
using Moongate.MigrationRunner.Tests.TestSupport;
using Moongate.Persistence.Migrations.Services;
using Moongate.Persistence.Migrations.Types.Migrations;
using Npgsql;

namespace Moongate.MigrationRunner.Tests.Integration;

[Collection(PostgresTestCollection.Name)]
public sealed class PostgreSqlMigrationRunnerTests : IClassFixture<PostgreSqlFixture>
{
    private readonly PostgreSqlFixture _postgres;

    public PostgreSqlMigrationRunnerTests(PostgreSqlFixture postgres)
    {
        _postgres = postgres;
    }

    [Fact]
    public async Task Apply_ReviewRequiredDraftStaysBlockedUntilExplicitReview()
    {
        await using var db = await _postgres.CreateDatabaseAsync();
        using var files = new MigrationFiles();
        files.Write("migrations/world/0001_draft.sql", "-- moongate:review-required\nCREATE TABLE reviewed(value integer);");

        for (var attempt = 0; attempt < 2; attempt++)
        {
            Assert.Throws<InvalidOperationException>(() => PostgreSqlMigrationRunner.Apply(
                    db.ConnectionString,
                    MigrationCatalog.Load(files.Core, null, MigrationTarget.World)
                )
            );
            Assert.False(await db.ScalarAsync<bool>("SELECT to_regclass('reviewed') IS NOT NULL"));
        }

        files.Write("migrations/world/0001_draft.sql", "CREATE TABLE reviewed(value integer);");
        Assert.Equal(
            1,
            PostgreSqlMigrationRunner.Apply(
                db.ConnectionString,
                MigrationCatalog.Load(files.Core, null, MigrationTarget.World)
            )
        );
    }

    [Fact]
    public async Task Apply_BlocksChangedHistoryBeforeAnyNewDdl()
    {
        await using var db = await _postgres.CreateDatabaseAsync();
        using var files = new MigrationFiles();
        files.Write("migrations/world/0001_create.sql", "CREATE TABLE sample (value integer);");
        PostgreSqlMigrationRunner.Apply(db.ConnectionString, MigrationCatalog.Load(files.Core, null, MigrationTarget.World));
        files.Write("migrations/world/0001_create.sql", "SELECT 1;");
        files.Write("migrations/world/0002_next.sql", "CREATE TABLE later (value integer);");
        Assert.Throws<InvalidOperationException>(() => PostgreSqlMigrationRunner.Apply(
                db.ConnectionString,
                MigrationCatalog.Load(files.Core, null, MigrationTarget.World)
            )
        );
        Assert.False(await db.ScalarAsync<bool>("SELECT to_regclass('later') IS NOT NULL"));
    }

    [Theory, InlineData("\r"), InlineData("\n"), InlineData("\r\n")]
    public async Task Apply_CommentLineEndingsCannotHideCommit(string lineEnding)
    {
        await using var db = await _postgres.CreateDatabaseAsync();
        using var files = new MigrationFiles();
        files.Write(
            "migrations/world/0001_commit.sql",
            "CREATE TABLE sample(value integer); -- comment" +
            lineEnding +
            "COMMIT; INSERT INTO nonexistent VALUES (1);"
        );
        Assert.Throws<InvalidOperationException>(() => PostgreSqlMigrationRunner.Apply(
                db.ConnectionString,
                MigrationCatalog.Load(files.Core, null, MigrationTarget.World)
            )
        );
        Assert.False(await db.ScalarAsync<bool>("SELECT to_regclass('sample') IS NOT NULL"));
        Assert.False(await db.ScalarAsync<bool>("SELECT to_regclass('moongate_migrations.history') IS NOT NULL"));
    }

    [Fact]
    public async Task Apply_ConcurrentRunsSerializeAndDoNotRepeatDml()
    {
        await using var db = await _postgres.CreateDatabaseAsync();
        using var files = new MigrationFiles();
        files.Write(
            "migrations/world/0001_create.sql",
            "CREATE TABLE sample (value integer); SELECT pg_sleep(0.2); INSERT INTO sample VALUES (1);"
        );
        var catalog = MigrationCatalog.Load(files.Core, null, MigrationTarget.World);
        var results = await Task.WhenAll(
            Task.Run(() => PostgreSqlMigrationRunner.Apply(db.ConnectionString, catalog)),
            Task.Run(() => PostgreSqlMigrationRunner.Apply(db.ConnectionString, catalog))
        );
        Assert.Equal(new[] { 0, 1 }, results.Order().ToArray());
        Assert.Equal(1L, await db.ScalarAsync<long>("SELECT count(*) FROM sample"));
    }

    [Fact]
    public async Task Apply_ExecutesDollarQuotedBlocksAndSqlLiterals()
    {
        await using var db = await _postgres.CreateDatabaseAsync();
        using var files = new MigrationFiles();
        files.Write(
            "migrations/world/0001_block.sql",
            "CREATE TABLE sample(value text); DO $body$ BEGIN INSERT INTO sample VALUES ('COMMIT; -- $literal$'); END $body$;"
        );
        PostgreSqlMigrationRunner.Apply(db.ConnectionString, MigrationCatalog.Load(files.Core, null, MigrationTarget.World));
        Assert.Equal("COMMIT; -- $literal$", await db.ScalarAsync<string>("SELECT value FROM sample"));
    }

    [Fact]
    public async Task Apply_RecordsSqlOnceAndReadsHistoryForTarget()
    {
        await using var db = await _postgres.CreateDatabaseAsync();
        using var files = new MigrationFiles();
        files.Write(
            "migrations/world/0001_create.sql",
            "CREATE TABLE sample (value integer); INSERT INTO sample VALUES (7);"
        );
        var catalog = MigrationCatalog.Load(files.Core, null, MigrationTarget.World);
        Assert.Equal(1, PostgreSqlMigrationRunner.Apply(db.ConnectionString, catalog));
        Assert.Equal(0, PostgreSqlMigrationRunner.Apply(db.ConnectionString, catalog));
        Assert.Equal(1L, await db.ScalarAsync<long>("SELECT count(*) FROM sample"));
        await using var connection = new NpgsqlConnection(db.ConnectionString);
        await connection.OpenAsync();
        var history = await MigrationHistory.ReadAsync(() => connection.CreateCommand(), MigrationTarget.World);
        Assert.Empty(MigrationHistory.Validate(catalog, history));
        Assert.Empty(await MigrationHistory.ReadAsync(() => connection.CreateCommand(), MigrationTarget.Auth));
    }

    [Theory, InlineData("COMMIT;"), InlineData("/* nested /* x */ comment */ END;"),
     InlineData("-- comment\nSTART TRANSACTION;"), InlineData("ROLLBACK;"), InlineData("PREPARE TRANSACTION 'name';")]
    public async Task Apply_RejectsTransactionControlBeforeAnyDdl(string sql)
    {
        await using var db = await _postgres.CreateDatabaseAsync();
        using var files = new MigrationFiles();
        files.Write("migrations/world/0001_create.sql", "CREATE TABLE sample (value integer); " + sql);
        Assert.Throws<InvalidOperationException>(() => PostgreSqlMigrationRunner.Apply(
                db.ConnectionString,
                MigrationCatalog.Load(files.Core, null, MigrationTarget.World)
            )
        );
        Assert.False(await db.ScalarAsync<bool>("SELECT to_regclass('sample') IS NOT NULL"));
    }

    [Fact]
    public async Task Apply_RollsBackAllPendingScriptsAndHistoryOnFailure()
    {
        await using var db = await _postgres.CreateDatabaseAsync();
        using var files = new MigrationFiles();
        files.Write("migrations/world/0001_create.sql", "CREATE TABLE sample (value integer);");
        files.Write("migrations/world/0002_fail.sql", "INSERT INTO nonexistent VALUES (1);");
        var error = Assert.Throws<InvalidOperationException>(() => PostgreSqlMigrationRunner.Apply(
                db.ConnectionString,
                MigrationCatalog.Load(files.Core, null, MigrationTarget.World)
            )
        );
        Assert.Contains("core/0002_fail.sql", error.Message);
        Assert.Contains("42P01", error.Message);
        Assert.False(await db.ScalarAsync<bool>("SELECT to_regclass('sample') IS NOT NULL"));
        Assert.False(await db.ScalarAsync<bool>("SELECT to_regclass('moongate_migrations.history') IS NOT NULL"));
    }
}
