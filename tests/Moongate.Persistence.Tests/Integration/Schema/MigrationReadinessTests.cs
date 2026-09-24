using Moongate.Persistence.Data.Config;
using Moongate.Persistence.Internal;
using Moongate.Persistence.Migrations.Services;
using Moongate.Persistence.Migrations.Types.Migrations;
using Moongate.Persistence.Tests.TestSupport.Persistence;
using Moongate.Persistence.Types.Persistence;

namespace Moongate.Persistence.Tests.Integration.Schema;

[Collection(PostgreSqlCollection.Name)]
public sealed class MigrationReadinessTests
{
    private readonly PostgreSqlFixture _postgres;

    public MigrationReadinessTests(PostgreSqlFixture postgres)
    {
        _postgres = postgres;
    }

    [Fact]
    public async Task InitializeAsync_ConnectionCheck_DoesNotActivateFilteredSqlTarget()
    {
        await using var db = await _postgres.CreateDatabaseAsync();
        using var files = new MigrationFiles();
        files.Write("migrations/world/0001_data.sql", "SELECT 42;");
        await using var coordinator = new PersistenceSchemaCoordinator(
            new(
                [new(PersistenceDatabaseTarget.Realm, db.ConnectionString)],
                migrationCatalogFactory: _ => MigrationCatalog.Load(files.Core, null, MigrationTarget.World),
                activateMigrationTarget: _ => false
            ),
            new()
        );

        await coordinator.InitializeAsync();

        Assert.True(coordinator.IsReady);
        Assert.Throws<InvalidOperationException>(() => coordinator.GetDatabase(PersistenceDatabaseTarget.Realm));
        Assert.False(await db.ScalarAsync<bool>("SELECT to_regclass('moongate_migrations.history') IS NOT NULL"));
    }

    [Fact]
    public async Task InitializeAsync_AppliedDataMigration_ValidatesChecksum()
    {
        await using var db = await _postgres.CreateDatabaseAsync();
        using var files = new MigrationFiles();
        files.Write("migrations/world/0001_data.sql", "SELECT 42;");
        var script = MigrationCatalog.Load(files.Core, null, MigrationTarget.World).Scripts.Single();
        await db.ExecuteAsync(
            "CREATE SCHEMA moongate_migrations; CREATE TABLE moongate_migrations.history (target text, component text, script text, checksum text);" +
            $"INSERT INTO moongate_migrations.history VALUES ('world', 'core', '0001_data.sql', '{script.Checksum}');"
        );

        await using (var ready = Create(db, files))
        {
            await ready.InitializeAsync();
            Assert.True(ready.IsReady);
        }

        files.Write("migrations/world/0001_data.sql", "SELECT 43;");
        await using var changed = Create(db, files);
        await Assert.ThrowsAsync<InvalidOperationException>(() => changed.InitializeAsync());
        Assert.False(changed.IsReady);
    }

    [Fact]
    public async Task InitializeAsync_DataOnlyMigrationWithoutEntities_BlocksReadiness()
    {
        await using var db = await _postgres.CreateDatabaseAsync();
        using var files = new MigrationFiles();
        files.Write("migrations/world/0001_data.sql", "SELECT 42;");
        await using var coordinator = Create(db, files);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.InitializeAsync());
        Assert.Contains("0001_data.sql", error.Message);
        Assert.False(coordinator.IsReady);
        Assert.False(await db.ScalarAsync<bool>("SELECT to_regclass('moongate_migrations.history') IS NOT NULL"));
    }

    [Fact]
    public async Task InitializeAsync_EmptyCatalogAndNoEntities_StillRequiresConfiguredDatabase()
    {
        using var files = new MigrationFiles();
        var options = new PostgreSqlPersistenceOptions(
            [new(PersistenceDatabaseTarget.Accounts, () => throw new InvalidOperationException("missing connection"))],
            migrationCatalogFactory: _ => MigrationCatalog.Load(files.Core, null, MigrationTarget.Auth)
        );
        await using var coordinator = new PersistenceSchemaCoordinator(options, new());
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.InitializeAsync());
        Assert.Contains("missing connection", error.Message);
        Assert.False(coordinator.IsReady);
    }

    private static PersistenceSchemaCoordinator Create(PostgreSqlTestDatabase db, MigrationFiles files)
    {
        var options = new PostgreSqlPersistenceOptions(
            [
                new(PersistenceDatabaseTarget.Realm, db.ConnectionString),
                new(PersistenceDatabaseTarget.Accounts, db.ConnectionString)
            ],
            migrationCatalogFactory: target => MigrationCatalog.Load(
                                         files.Core,
                                         null,
                                         target == PersistenceDatabaseTarget.Accounts
                                             ? MigrationTarget.Auth
                                             : MigrationTarget.World
                                     )
        );

        return new(options, new());
    }
}
