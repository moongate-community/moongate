using Moongate.Persistence.Internal;
using Moongate.Persistence.Tests.TestSupport.Persistence;
using Moongate.Persistence.Types.Persistence;
using Npgsql;

namespace Moongate.Persistence.Tests.Integration.Schema;

[Collection(PostgreSqlCollection.Name)]
public sealed class DevelopmentSchemaAssessmentTests
{
    private readonly PostgreSqlFixture _fixture;

    public DevelopmentSchemaAssessmentTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task AssessAsync_NewTableIndexes_ApplyAutomaticallyAndRemainStable()
    {
        await using var db = await _fixture.CreateDatabaseAsync();
        using var database = PostgreSqlDatabase.Create(new(PersistenceDatabaseTarget.Accounts, db.ConnectionString));
        Type[] entities = [typeof(DevelopmentIndexedAccountEntity)];
        var created = await DevelopmentSchemaAssessor.AssessAsync(database, entities, CancellationToken.None);
        Assert.False(created.RequiresReview, created.Ddl);
        await db.ExecuteAsync(created.Ddl);
        Assert.Equal(
            3L,
            await db.ScalarAsync<long>(
                "SELECT count(*) FROM pg_indexes WHERE schemaname = 'auth' AND indexname IN " +
                "('ux_development_indexed_username', 'ix_development_indexed_lookup', 'ix_development_indexed_id')"
            )
        );
        await db.ExecuteAsync("INSERT INTO auth.development_indexed_accounts VALUES (1, 'same');");
        var duplicate = await Assert.ThrowsAsync<PostgresException>(
                            () => db.ExecuteAsync("INSERT INTO auth.development_indexed_accounts VALUES (2, 'same');")
                        );
        Assert.Equal(PostgresErrorCodes.UniqueViolation, duplicate.SqlState);
        var unchanged = await DevelopmentSchemaAssessor.AssessAsync(database, entities, CancellationToken.None);
        Assert.False(unchanged.RequiresReview, unchanged.Ddl);
        Assert.True(string.IsNullOrWhiteSpace(unchanged.Ddl), unchanged.Ddl);
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task AssessAsync_ExistingTableIndexes_RequireReviewEvenAlongsideNewTable(bool includeNewTable)
    {
        await using var db = await _fixture.CreateDatabaseAsync();
        await db.ExecuteAsync(
            "CREATE SCHEMA auth; CREATE TABLE auth.development_indexed_accounts " +
            "(id bigint NOT NULL PRIMARY KEY, username varchar(255) NOT NULL); " +
            "INSERT INTO auth.development_indexed_accounts VALUES (1, 'same'), (2, 'same');"
        );
        using var database = PostgreSqlDatabase.Create(new(PersistenceDatabaseTarget.Accounts, db.ConnectionString));
        Type[] entities = includeNewTable
                              ? [typeof(DevelopmentIndexedAccountEntity), typeof(DevelopmentAccountEntity)]
                              : [typeof(DevelopmentIndexedAccountEntity)];
        var result = await DevelopmentSchemaAssessor.AssessAsync(database, entities, CancellationToken.None);
        Assert.Contains("CREATE UNIQUE INDEX", result.Ddl);
        Assert.True(result.RequiresReview, result.Ddl);
        Assert.Equal(2L, await db.ScalarAsync<long>("SELECT count(*) FROM auth.development_indexed_accounts"));
    }

    [Fact]
    public async Task AssessAsync_StringDefault_PostgreSqlCastDoesNotGenerateRepeatedMigration()
    {
        await using var db = await _fixture.CreateDatabaseAsync();
        using var database = PostgreSqlDatabase.Create(new(PersistenceDatabaseTarget.Accounts, db.ConnectionString));
        var created = await DevelopmentSchemaAssessor.AssessAsync(
                          database,
                          [typeof(DevelopmentStringDefaultEntity)],
                          CancellationToken.None
                      );
        Assert.False(created.RequiresReview, created.Ddl);
        await db.ExecuteAsync(created.Ddl);
        var unchanged = await DevelopmentSchemaAssessor.AssessAsync(
                            database,
                            [typeof(DevelopmentStringDefaultEntity)],
                            CancellationToken.None
                        );
        Assert.False(unchanged.RequiresReview, unchanged.Ddl);
        Assert.True(string.IsNullOrWhiteSpace(unchanged.Ddl), unchanged.Ddl);
    }

    [Theory, InlineData("DEFAULT 6", false), InlineData("", false), InlineData("DEFAULT 7", true)]
    public async Task AssessAsync_ExistingDefaultChangeOrRemoval_IsReviewableAndStable(string databaseDefault, bool remove)
    {
        await using var db = await _fixture.CreateDatabaseAsync();
        await db.ExecuteAsync(
            "CREATE SCHEMA auth; CREATE TABLE auth.development_defaults(id bigint PRIMARY KEY, level int4 NOT NULL " +
            databaseDefault +
            "); INSERT INTO auth.development_defaults VALUES(1, 42);"
        );
        using var database = PostgreSqlDatabase.Create(new(PersistenceDatabaseTarget.Accounts, db.ConnectionString));
        Type[] entities = [remove ? typeof(DevelopmentNoDefaultEntity) : typeof(DevelopmentDefaultEntity)];
        var result = await DevelopmentSchemaAssessor.AssessAsync(database, entities, CancellationToken.None);
        Assert.True(result.RequiresReview);
        Assert.Contains(remove ? "DROP DEFAULT" : "SET DEFAULT", result.Ddl);
        await db.ExecuteAsync(result.Ddl);
        Assert.Equal(42, await db.ScalarAsync<int>("SELECT level FROM auth.development_defaults WHERE id=1"));
        var unchanged = await DevelopmentSchemaAssessor.AssessAsync(database, entities, CancellationToken.None);
        Assert.False(unchanged.RequiresReview, unchanged.Ddl);
        Assert.True(string.IsNullOrWhiteSpace(unchanged.Ddl), unchanged.Ddl);
    }

    [Fact]
    public async Task AssessAsync_ExplicitRename_DraftAppliesOnceWithoutDroppingRenamedColumn()
    {
        await using var db = await _fixture.CreateDatabaseAsync();
        await db.ExecuteAsync(
            "CREATE SCHEMA auth; CREATE TABLE auth.development_rename(id bigint PRIMARY KEY, old_level int4); INSERT INTO auth.development_rename VALUES(1, 42);"
        );
        using var database = PostgreSqlDatabase.Create(new(PersistenceDatabaseTarget.Accounts, db.ConnectionString));
        var result = await DevelopmentSchemaAssessor.AssessAsync(
                         database,
                         [typeof(DevelopmentRenamedEntity)],
                         CancellationToken.None
                     );
        Assert.True(result.RequiresReview);
        Assert.Contains("RENAME COLUMN", result.Ddl);
        Assert.DoesNotContain("DROP COLUMN", result.Ddl);
        await db.ExecuteAsync(result.Ddl);
        Assert.Equal(42, await db.ScalarAsync<int>("SELECT level FROM auth.development_rename WHERE id=1"));
        var unchanged = await DevelopmentSchemaAssessor.AssessAsync(
                            database,
                            [typeof(DevelopmentRenamedEntity)],
                            CancellationToken.None
                        );
        Assert.False(unchanged.RequiresReview);
        Assert.True(string.IsNullOrWhiteSpace(unchanged.Ddl), unchanged.Ddl);
    }

    [Fact]
    public async Task AssessAsync_NewTableWithExplicitDefault_HasStableDefaultsAfterCreation()
    {
        await using var db = await _fixture.CreateDatabaseAsync();
        using var database = PostgreSqlDatabase.Create(new(PersistenceDatabaseTarget.Accounts, db.ConnectionString));
        var created = await DevelopmentSchemaAssessor.AssessAsync(
                          database,
                          [typeof(DevelopmentDefaultEntity)],
                          CancellationToken.None
                      );
        Assert.False(created.RequiresReview);
        await db.ExecuteAsync(created.Ddl);
        var unchanged = await DevelopmentSchemaAssessor.AssessAsync(
                            database,
                            [typeof(DevelopmentDefaultEntity)],
                            CancellationToken.None
                        );
        Assert.False(unchanged.RequiresReview, unchanged.Ddl);
        Assert.True(string.IsNullOrWhiteSpace(unchanged.Ddl), unchanged.Ddl);
    }

    [Fact]
    public async Task AssessAsync_ExplicitLiteralDefaultBackfillsNewRequiredColumn()
    {
        await using var db = await _fixture.CreateDatabaseAsync();
        await db.ExecuteAsync(
            "CREATE SCHEMA auth; CREATE TABLE auth.development_defaults(id bigint NOT NULL PRIMARY KEY); INSERT INTO auth.development_defaults VALUES (1);"
        );
        using var database = PostgreSqlDatabase.Create(new(PersistenceDatabaseTarget.Accounts, db.ConnectionString));
        var result = await DevelopmentSchemaAssessor.AssessAsync(
                         database,
                         [typeof(DevelopmentDefaultEntity)],
                         CancellationToken.None
                     );
        Assert.False(result.RequiresReview, result.Ddl);
        await db.ExecuteAsync(result.Ddl);
        Assert.Equal(7, await db.ScalarAsync<int>("SELECT level FROM auth.development_defaults WHERE id=1"));
    }

    [Theory, InlineData("username text, last_login_at timestamp"),
     InlineData("username varchar(255) NOT NULL, last_login_at timestamp")]
    public async Task AssessAsync_ExistingTypeOrNullabilityChange_RequiresReview(string columns)
    {
        await using var db = await _fixture.CreateDatabaseAsync();
        await db.ExecuteAsync(
            "CREATE SCHEMA auth; CREATE TABLE auth.development_accounts(id bigint NOT NULL PRIMARY KEY, " + columns + ");"
        );
        using var database = PostgreSqlDatabase.Create(new(PersistenceDatabaseTarget.Accounts, db.ConnectionString));
        var result = await DevelopmentSchemaAssessor.AssessAsync(
                         database,
                         [typeof(DevelopmentAccountEntity)],
                         CancellationToken.None
                     );
        Assert.True(result.RequiresReview, result.Ddl);
    }

    [Fact]
    public async Task AssessAsync_RequiredColumnWithoutExplicitBackfill_RequiresReview()
    {
        await using var db = await _fixture.CreateDatabaseAsync();
        await db.ExecuteAsync("CREATE SCHEMA auth; CREATE TABLE auth.development_required(id bigint NOT NULL PRIMARY KEY);");
        using var database = PostgreSqlDatabase.Create(new(PersistenceDatabaseTarget.Accounts, db.ConnectionString));
        var result = await DevelopmentSchemaAssessor.AssessAsync(
                         database,
                         [typeof(DevelopmentRequiredEntity)],
                         CancellationToken.None
                     );
        Assert.True(result.RequiresReview, result.Ddl);
    }

    [Fact]
    public async Task AssessAsync_NullableAddition_DoesNotRewriteRowsOrFireUpdateTriggers()
    {
        await using var db = await _fixture.CreateDatabaseAsync();
        await db.ExecuteAsync(
            """
            CREATE SCHEMA auth;
            CREATE TABLE auth.development_accounts(id bigint NOT NULL PRIMARY KEY, username varchar(255));
            INSERT INTO auth.development_accounts VALUES (1, 'existing');
            CREATE FUNCTION auth.reject_update() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN RAISE EXCEPTION 'Unexpected UPDATE'; END $$;
            CREATE TRIGGER reject_update BEFORE UPDATE ON auth.development_accounts FOR EACH ROW EXECUTE FUNCTION auth.reject_update();
            """
        );
        using var database = PostgreSqlDatabase.Create(new(PersistenceDatabaseTarget.Accounts, db.ConnectionString));
        var result = await DevelopmentSchemaAssessor.AssessAsync(
                         database,
                         [typeof(DevelopmentAccountEntity)],
                         CancellationToken.None
                     );
        Assert.False(result.RequiresReview);
        await db.ExecuteAsync(result.Ddl);
        Assert.Equal(
            "existing",
            await db.ScalarAsync<string>("SELECT username FROM auth.development_accounts WHERE id = 1")
        );
        Assert.Null(await db.ScalarAsync<object>("SELECT last_login_at FROM auth.development_accounts WHERE id = 1"));
        var unchanged = await DevelopmentSchemaAssessor.AssessAsync(
                            database,
                            [typeof(DevelopmentAccountEntity)],
                            CancellationToken.None
                        );
        Assert.False(unchanged.RequiresReview);
        Assert.True(string.IsNullOrWhiteSpace(unchanged.Ddl));
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task AssessAsync_NewTableOrNullableColumn_IsAutomatic(bool existing)
    {
        await using var db = await _fixture.CreateDatabaseAsync();

        if (existing)
        {
            await db.ExecuteAsync(
                "CREATE SCHEMA auth; CREATE TABLE auth.development_accounts(id bigint NOT NULL PRIMARY KEY, username varchar(255));"
            );
        }

        using var database = PostgreSqlDatabase.Create(new(PersistenceDatabaseTarget.Accounts, db.ConnectionString));
        var result = await DevelopmentSchemaAssessor.AssessAsync(
                         database,
                         [typeof(DevelopmentAccountEntity)],
                         CancellationToken.None
                     );
        Assert.False(result.RequiresReview, result.Ddl);
        Assert.NotEmpty(result.Ddl);
    }

    [Fact]
    public async Task AssessAsync_RemovedColumnRequiresReviewEvenWithoutProviderDropDdl()
    {
        await using var db = await _fixture.CreateDatabaseAsync();
        await db.ExecuteAsync(
            "CREATE SCHEMA auth; CREATE TABLE auth.development_accounts(id bigint NOT NULL PRIMARY KEY, username varchar(255), last_login_at timestamp, old_name text);"
        );
        using var database = PostgreSqlDatabase.Create(new(PersistenceDatabaseTarget.Accounts, db.ConnectionString));
        var result = await DevelopmentSchemaAssessor.AssessAsync(
                         database,
                         [typeof(DevelopmentAccountEntity)],
                         CancellationToken.None
                     );
        Assert.True(result.RequiresReview);
        Assert.Contains("old_name", result.Ddl);
    }
}
