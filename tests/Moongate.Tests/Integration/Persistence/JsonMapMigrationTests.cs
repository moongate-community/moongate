using Moongate.Persistence.Migrations.Services;
using Moongate.Persistence.Types.Persistence;
using Moongate.Tests.TestSupport.Persistence;

namespace Moongate.Tests.Integration.Persistence;

[Collection(PostgresTestCollection.Name)]
public sealed class JsonMapMigrationTests
{
    [Theory, InlineData(PersistenceDatabaseTarget.Accounts), InlineData(PersistenceDatabaseTarget.Realm)]
    public async Task InitializeAsync_NewJsonbEntity_GeneratesAndAppliesStableMigration(PersistenceDatabaseTarget target)
    {
        await using var database = await new PostgreSqlFixture().CreateDatabaseAsync();
        using var fixture = new DevelopmentMigrationFixture(database.ConnectionString);

        for (var startup = 0; startup < 2; startup++)
        {
            await using var coordinator = fixture.Create(typeof(DevelopmentJsonEntity), target);
            await coordinator.InitializeAsync();
            Assert.True(coordinator.IsReady);
        }

        var directory = target == PersistenceDatabaseTarget.Accounts ? "auth" : "world";
        var file = Assert.Single(Directory.GetFiles(Path.Combine(fixture.Migrations, directory), "*.sql"));
        Assert.DoesNotContain(MigrationReviewGuard.Marker, await File.ReadAllTextAsync(file));
        Assert.Equal(1L, await database.ScalarAsync<long>("SELECT count(*) FROM moongate_migrations.history"));
        Assert.Equal(
            "jsonb",
            await database.ScalarAsync<string>(
                "SELECT data_type FROM information_schema.columns WHERE table_schema = 'host_test' " +
                "AND table_name = 'items' AND column_name = 'progress'"
            )
        );
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task InitializeAsync_AddNullableJsonbColumn_PreservesRowsAndDoesNotRepeatMigration(bool initializeEmpty)
    {
        await using var database = await new PostgreSqlFixture().CreateDatabaseAsync();
        using var fixture = new DevelopmentMigrationFixture(database.ConnectionString);
        await using (var original = fixture.Create(typeof(TestEntity)))
        {
            await original.InitializeAsync();
        }

        await database.ExecuteAsync(
            """
            INSERT INTO host_test.items (id, name) VALUES (42, 'existing');
            CREATE FUNCTION host_test.reject_update() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN RAISE EXCEPTION 'Unexpected JSON backfill'; END $$;
            CREATE TRIGGER reject_update BEFORE UPDATE ON host_test.items
            FOR EACH ROW EXECUTE FUNCTION host_test.reject_update();
            """
        );

        for (var startup = 0; startup < 2; startup++)
        {
            await using var coordinator = fixture.Create(
                initializeEmpty ? typeof(DevelopmentInitializedJsonEntity) : typeof(DevelopmentJsonEntity)
            );
            await coordinator.InitializeAsync();
            Assert.True(coordinator.IsReady);
        }

        var files = Directory.GetFiles(Path.Combine(fixture.Migrations, "world"), "*.sql");
        Assert.Equal(2, files.Length);

        foreach (var file in files)
        {
            Assert.DoesNotContain(MigrationReviewGuard.Marker, await File.ReadAllTextAsync(file));
        }

        Assert.Equal(2L, await database.ScalarAsync<long>("SELECT count(*) FROM moongate_migrations.history"));
        Assert.Equal("existing", await database.ScalarAsync<string>("SELECT name FROM host_test.items WHERE id = 42"));
        Assert.True(await database.ScalarAsync<bool>("SELECT progress IS NULL FROM host_test.items WHERE id = 42"));
    }
}
