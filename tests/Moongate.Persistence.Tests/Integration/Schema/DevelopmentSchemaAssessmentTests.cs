using Moongate.Persistence.Internal;
using Moongate.Persistence.Tests.TestSupport.Persistence;
using Moongate.Persistence.Types.Persistence;

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
