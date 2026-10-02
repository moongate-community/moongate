using Moongate.Persistence.Extensions;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Tests.TestSupport.Persistence;
using Npgsql;

namespace Moongate.Tests.Integration.Server.Ultima.Entities;

[Collection(PostgresTestCollection.Name)]
public sealed class WorldMigrationsTests
{
    [Fact]
    public async Task ShippedWorldSql_MatchesMobileAndItemEntities()
    {
        await using var host = await HostPersistenceFixture.CreateAsync(false);
        host.Container.AddPersistenceWorld<MobileEntity>().AddPersistenceWorld<ItemEntity>();
        await CoreMigrationFiles.ApplyAsync(host.Database, "world");

        // Without development migrations, startup compares the model with the database and fails on any difference.
        await host.Owner.InitializeAsync();
    }

    [Fact]
    public async Task ACommentThatDiffersFromTheModel_DoesNotStopStartup()
    {
        await using var host = await HostPersistenceFixture.CreateAsync(false);
        host.Container.AddPersistenceWorld<MobileEntity>().AddPersistenceWorld<ItemEntity>();
        await CoreMigrationFiles.ApplyAsync(host.Database, "world");

        // A single-file build cannot read the XML docs the model takes its comments from: it sees none.
        await host.Database.ExecuteAsync("COMMENT ON COLUMN world.items.template_id IS 'edited'");
        await host.Database.ExecuteAsync("COMMENT ON COLUMN world.items.x IS 'only in the database'");

        await host.Owner.InitializeAsync();
    }

    [Fact]
    public async Task ANotorietyOutsideTheEnum_IsRejectedByTheDatabase()
    {
        await using var host = await HostPersistenceFixture.CreateAsync(false);
        await CoreMigrationFiles.ApplyAsync(host.Database, "world");

        var exception = await Record.ExceptionAsync(() => host.Database.ExecuteAsync(
            "INSERT INTO world.mobiles (id, name, gender, race, body, skin_hue, strength, dexterity, intelligence, hair_style, " +
            "hair_hue, beard_style, beard_hue, created_at, x, y, z, map, hits, hits_max, mana, mana_max, stamina, stamina_max, " +
            "fame, karma, armor, resist_physical, resist_fire, resist_cold, resist_poison, resist_energy, direction, notoriety) " +
            "VALUES (5, 'a', 0, 0, 400, 0, 0, 0, 0, 0, 0, 0, 0, now(), 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 4, 9)"
        ));

        Assert.IsType<PostgresException>(exception);
        Assert.Equal("ck_mobiles_notoriety", ((PostgresException)exception!).ConstraintName);
    }

    [Fact]
    public async Task ADirectionOutsideTheEnum_IsRejectedByTheDatabase()
    {
        await using var host = await HostPersistenceFixture.CreateAsync(false);
        await CoreMigrationFiles.ApplyAsync(host.Database, "world");

        var exception = await Record.ExceptionAsync(() => host.Database.ExecuteAsync(
            "INSERT INTO world.mobiles (id, name, gender, race, body, skin_hue, strength, dexterity, intelligence, hair_style, " +
            "hair_hue, beard_style, beard_hue, created_at, x, y, z, map, hits, hits_max, mana, mana_max, stamina, stamina_max, " +
            "fame, karma, armor, resist_physical, resist_fire, resist_cold, resist_poison, resist_energy, direction) " +
            "VALUES (5, 'a', 0, 0, 400, 0, 0, 0, 0, 0, 0, 0, 0, now(), 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 132)"
        ));

        Assert.IsType<PostgresException>(exception);
        Assert.Equal("ck_mobiles_direction", ((PostgresException)exception!).ConstraintName);
    }
}
