using Moongate.Persistence.Types.Persistence;
using Moongate.Server.Ultima.Entities.Auth;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Tests.TestSupport.Persistence;

namespace Moongate.Tests.Integration.Persistence;

/// <summary>
///     A development root holds the shipped SQL (mgctl copies it) with automatic generation on: once that SQL is
///     applied, the entities must match it, or every start writes a draft that needs review and stops.
/// </summary>
[Collection(PostgresTestCollection.Name)]
public sealed class CoreMigrationsDevelopmentTests
{
    [Fact]
    public async Task TheShippedAuthSql_LeavesNothingToGenerate()
    {
        await AssertNothingGeneratedAsync("auth", "AccountMigrations", PersistenceDatabaseTarget.Accounts, typeof(AccountEntity));
    }

    [Fact]
    public async Task TheShippedWorldSql_LeavesNothingToGenerate()
    {
        await AssertNothingGeneratedAsync(
            "world",
            "WorldMigrations",
            PersistenceDatabaseTarget.Realm,
            typeof(MobileEntity),
            typeof(ItemEntity),
            typeof(WorldStateEntity),
            typeof(JailSentenceEntity)
        );
    }

    [Fact]
    public async Task ACommentTheModelDoesNotHave_IsNeitherDroppedNorReviewed()
    {
        await AssertNothingGeneratedAsync(
            "world",
            "WorldMigrations",
            PersistenceDatabaseTarget.Realm,
            "COMMENT ON COLUMN world.items.x IS 'only in the database'",
            typeof(MobileEntity),
            typeof(ItemEntity),
            typeof(WorldStateEntity),
            typeof(JailSentenceEntity)
        );
    }

    private static Task AssertNothingGeneratedAsync(string target, string shipped, PersistenceDatabaseTarget database, params Type[] entities)
    {
        return AssertNothingGeneratedAsync(target, shipped, database, null, entities);
    }

    private static async Task AssertNothingGeneratedAsync(
        string target,
        string shipped,
        PersistenceDatabaseTarget database,
        string? afterwards,
        params Type[] entities
    )
    {
        await using var db = await new PostgreSqlFixture().CreateDatabaseAsync();
        using var fixture = new DevelopmentMigrationFixture(db.ConnectionString);
        var catalog = Path.Combine(fixture.Migrations, target);
        Directory.CreateDirectory(catalog);

        foreach (var file in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, shipped), "*.sql"))
        {
            File.Copy(file, Path.Combine(catalog, Path.GetFileName(file)));
        }

        var shippedFiles = Directory.GetFiles(catalog, "*.sql").Select(Path.GetFileName).Order().ToArray();

        await using (var coordinator = fixture.Create(database, entities))
        {
            await coordinator.InitializeAsync();
        }

        if (afterwards is not null)
        {
            await db.ExecuteAsync(afterwards);

            await using var again = fixture.Create(database, entities);
            await again.InitializeAsync();
        }

        Assert.Equal(shippedFiles, Directory.GetFiles(catalog, "*.sql").Select(Path.GetFileName).Order().ToArray());
    }
}
