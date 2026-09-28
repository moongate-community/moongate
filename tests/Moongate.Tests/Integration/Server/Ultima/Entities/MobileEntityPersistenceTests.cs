using DryIoc;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Core.Types.Geometry;
using Moongate.Persistence.Extensions;
using Moongate.Persistence.Interfaces;
using Moongate.Persistence.Types.Persistence;
using Moongate.Server.Ultima.Data.Mobiles;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Types.Mobiles;
using Moongate.Tests.TestSupport.Persistence;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Integration.Server.Ultima.Entities;

[Collection(PostgresTestCollection.Name)]
public sealed class MobileEntityPersistenceTests
{
    [Fact]
    public async Task Location_And_Map_AreStoredAsIntegerColumnsAndReadBack()
    {
        await using var database = await new PostgreSqlFixture().CreateDatabaseAsync();
        using var fixture = new DevelopmentMigrationFixture(database.ConnectionString);
        await using var coordinator = fixture.Create(typeof(MobileEntity));
        await coordinator.InitializeAsync();
        var orm = coordinator.GetDatabase(PersistenceDatabaseTarget.Realm).Orm;
        var character = new MobileEntity
        {
            Id = new(1),
            AccountId = new(42),
            Name = "Aria",
            Location = new(738, 3486, -19),
            Map = MapType.TerMur
        };

        await orm.Insert(character).ExecuteAffrowsAsync();
        var loaded = await orm.Select<MobileEntity>().Where(entity => entity.Name == "Aria").FirstAsync();

        Assert.NotNull(loaded);
        Assert.Equal(new Point3D(738, 3486, -19), loaded.Location);
        Assert.Equal(MapType.TerMur, loaded.Map);
        Assert.Equal(5, await database.ScalarAsync<int>("SELECT map::int FROM world.mobiles"));
        Assert.Equal(-19, await database.ScalarAsync<int>("SELECT z FROM world.mobiles"));

        foreach (var column in new[] { "x", "y", "z", "map" })
        {
            Assert.Contains(
                await database.ScalarAsync<string>(
                    "SELECT data_type FROM information_schema.columns WHERE table_schema = 'world' " +
                    $"AND table_name = 'mobiles' AND column_name = '{column}'"
                ),
                new[] { "integer", "smallint" }
            );
        }

        Assert.Equal(
            0L,
            await database.ScalarAsync<long>(
                "SELECT count(*) FROM information_schema.columns WHERE table_schema = 'world' " +
                "AND table_name = 'mobiles' AND column_name = 'location'"
            )
        );
    }

    [Fact]
    public async Task CreationFields_RoundTrip_WithHuesAsIntegersAndEnumsAsSmallints()
    {
        await using var database = await new PostgreSqlFixture().CreateDatabaseAsync();
        using var fixture = new DevelopmentMigrationFixture(database.ConnectionString);
        await using var coordinator = fixture.Create(typeof(MobileEntity));
        await coordinator.InitializeAsync();
        var orm = coordinator.GetDatabase(PersistenceDatabaseTarget.Realm).Orm;
        var createdAt = new DateTime(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc);
        var character = new MobileEntity
        {
            Id = new(1),
            AccountId = new(42),
            Name = "Aria",
            Gender = GenderType.Female,
            Race = RaceType.Elf,
            Body = 606,
            SkinHue = new(0x83EA),
            Strength = 35,
            Dexterity = 35,
            Intelligence = 20,
            HairStyle = 0x2FC1,
            HairHue = new(0xFFFF),
            BeardStyle = 0,
            BeardHue = Hue.None,
            CreatedAt = createdAt
        };

        await orm.Insert(character).ExecuteAffrowsAsync();
        var loaded = await orm.Select<MobileEntity>().Where(entity => entity.Name == "Aria").FirstAsync();

        Assert.NotNull(loaded);
        Assert.Equal(GenderType.Female, loaded.Gender);
        Assert.Equal(RaceType.Elf, loaded.Race);
        Assert.Equal(606, loaded.Body);
        Assert.Equal(new Hue(0x83EA), loaded.SkinHue);
        Assert.Equal((35, 35, 20), (loaded.Strength, loaded.Dexterity, loaded.Intelligence));
        Assert.Equal(0x2FC1, loaded.HairStyle);
        Assert.Equal(new Hue(0xFFFF), loaded.HairHue);
        Assert.Equal(0, loaded.BeardStyle);
        Assert.Equal(Hue.None, loaded.BeardHue);
        Assert.Equal(createdAt, loaded.CreatedAt);
        Assert.Equal(DateTimeKind.Utc, loaded.CreatedAt.Kind);
        Assert.Equal(0x83EA, await database.ScalarAsync<int>("SELECT skin_hue FROM world.mobiles"));
        Assert.Equal(
            "integer",
            await database.ScalarAsync<string>(
                "SELECT data_type FROM information_schema.columns WHERE table_schema = 'world' " +
                "AND table_name = 'mobiles' AND column_name = 'hair_hue'"
            )
        );
        Assert.Equal(
            "smallint",
            await database.ScalarAsync<string>(
                "SELECT data_type FROM information_schema.columns WHERE table_schema = 'world' " +
                "AND table_name = 'mobiles' AND column_name = 'race'"
            )
        );
    }

    [Fact]
    public async Task Skills_RoundTripAsAJsonbList()
    {
        await using var database = await new PostgreSqlFixture().CreateDatabaseAsync();
        using var fixture = new DevelopmentMigrationFixture(database.ConnectionString);
        await using var coordinator = fixture.Create(typeof(MobileEntity));
        await coordinator.InitializeAsync();
        var orm = coordinator.GetDatabase(PersistenceDatabaseTarget.Realm).Orm;
        var character = new MobileEntity
        {
            Id = new(1),
            Name = "Aria",
            Skills =
            [
                new() { Skill = SkillType.Magery, Base = 500 },
                new() { Skill = SkillType.Meditation, Base = 300, Cap = 1200, Lock = SkillLockType.Locked }
            ]
        };

        await orm.Insert(character).ExecuteAffrowsAsync();
        var loaded = await orm.Select<MobileEntity>().FirstAsync();

        Assert.Equal(
            [(SkillType.Magery, 500, 1000, SkillLockType.Up), (SkillType.Meditation, 300, 1200, SkillLockType.Locked)],
            loaded.Skills.Select(skill => (skill.Skill, skill.Base, skill.Cap, skill.Lock))
        );
        Assert.Equal(
            "jsonb",
            await database.ScalarAsync<string>(
                "SELECT data_type FROM information_schema.columns WHERE table_schema = 'world' " +
                "AND table_name = 'mobiles' AND column_name = 'skills'"
            )
        );
        Assert.Equal(2, await database.ScalarAsync<int>("SELECT jsonb_array_length(skills) FROM world.mobiles"));
    }

    [Fact]
    public async Task Skills_EmptyList_RoundTripsAsEmpty()
    {
        await using var database = await new PostgreSqlFixture().CreateDatabaseAsync();
        using var fixture = new DevelopmentMigrationFixture(database.ConnectionString);
        await using var coordinator = fixture.Create(typeof(MobileEntity));
        await coordinator.InitializeAsync();
        var orm = coordinator.GetDatabase(PersistenceDatabaseTarget.Realm).Orm;

        await orm.Insert(new MobileEntity { Id = new(1), Name = "Aria" }).ExecuteAffrowsAsync();

        Assert.Empty((await orm.Select<MobileEntity>().FirstAsync()).Skills);
    }

    [Fact]
    public async Task Npcs_WithTheSameName_AreStoredSideBySide()
    {
        await using var database = await new PostgreSqlFixture().CreateDatabaseAsync();
        using var fixture = new DevelopmentMigrationFixture(database.ConnectionString);
        await using var coordinator = fixture.Create(typeof(MobileEntity));
        await coordinator.InitializeAsync();
        var orm = coordinator.GetDatabase(PersistenceDatabaseTarget.Realm).Orm;

        await orm.Insert(new MobileEntity { Id = new(1), Name = "a guard" }).ExecuteAffrowsAsync();
        await orm.Insert(new MobileEntity { Id = new(2), Name = "a guard" }).ExecuteAffrowsAsync();

        Assert.Equal(2L, await database.ScalarAsync<long>("SELECT count(*) FROM world.mobiles WHERE name = 'a guard'"));
    }

    [Fact]
    public async Task NpcFields_AndProps_RoundTrip()
    {
        await using var database = await new PostgreSqlFixture().CreateDatabaseAsync();
        using var fixture = new DevelopmentMigrationFixture(database.ConnectionString);
        await using var coordinator = fixture.Create(typeof(MobileEntity));
        await coordinator.InitializeAsync();
        var orm = coordinator.GetDatabase(PersistenceDatabaseTarget.Realm).Orm;
        var orc = new MobileEntity
        {
            Id = new(7), Name = "an orc", TemplateId = "orc", Title = "the Brute", Notoriety = NotorietyType.Murderer,
            Hits = 50, HitsMax = 60, Mana = 10, ManaMax = 20, Stamina = 30, StaminaMax = 40,
            Fame = 1500, Karma = -1500, Armor = 28,
            ResistPhysical = 25, ResistFire = 20, ResistCold = 10, ResistPoison = 15, ResistEnergy = 22
        };
        orc.SetProp("quest_step", 3);

        await orm.Insert(orc).ExecuteAffrowsAsync();
        var loaded = await orm.Select<MobileEntity>().Where(m => m.Id == orc.Id).FirstAsync();

        Assert.Equal(("orc", "the Brute", (NotorietyType?)NotorietyType.Murderer), (loaded.TemplateId, loaded.Title, loaded.Notoriety));
        Assert.Equal((50, 60, 10, 20, 30, 40), (loaded.Hits, loaded.HitsMax, loaded.Mana, loaded.ManaMax, loaded.Stamina, loaded.StaminaMax));
        Assert.Equal((1500, -1500, 28), (loaded.Fame, loaded.Karma, loaded.Armor));
        Assert.Equal((25, 20, 10, 15, 22), (loaded.ResistPhysical, loaded.ResistFire, loaded.ResistCold, loaded.ResistPoison, loaded.ResistEnergy));
        Assert.Equal(3, loaded.GetProp<int>("quest_step"));
    }

    [Fact]
    public void IsNpc_IsTrueOnlyWithoutAnAccount()
    {
        Assert.True(new MobileEntity().IsNpc);
        Assert.False(new MobileEntity { AccountId = new(42) }.IsNpc);
    }

    [Fact]
    public async Task AccountId_NullForAnNpcAndSetForAPlayer_RoundTrips()
    {
        await using var database = await new PostgreSqlFixture().CreateDatabaseAsync();
        using var fixture = new DevelopmentMigrationFixture(database.ConnectionString);
        await using var coordinator = fixture.Create(typeof(MobileEntity));
        await coordinator.InitializeAsync();
        var orm = coordinator.GetDatabase(PersistenceDatabaseTarget.Realm).Orm;

        await orm.Insert(new MobileEntity { Id = new(1), Name = "a guard" }).ExecuteAffrowsAsync();
        await orm.Insert(new MobileEntity { Id = new(2), Name = "Aria", AccountId = new(42) }).ExecuteAffrowsAsync();
        var guardId = new Serial(1);
        var ariaId = new Serial(2);
        var guard = await orm.Select<MobileEntity>().Where(entity => entity.Id == guardId).FirstAsync();
        var aria = await orm.Select<MobileEntity>().Where(entity => entity.Id == ariaId).FirstAsync();

        Assert.Null(guard.AccountId);
        Assert.True(guard.IsNpc);
        Assert.Equal(new Serial(42), aria.AccountId);
        Assert.False(aria.IsNpc);
        Assert.True(await database.ScalarAsync<bool>("SELECT account_id IS NULL FROM world.mobiles WHERE id = 1"));
        Assert.Equal(
            "YES",
            await database.ScalarAsync<string>(
                "SELECT is_nullable FROM information_schema.columns WHERE table_schema = 'world' " +
                "AND table_name = 'mobiles' AND column_name = 'account_id'"
            )
        );
    }

    [Fact]
    public void Location_SetsAndReadsTheThreeCoordinates()
    {
        var character = new MobileEntity { Location = new(1, 2, 3) };

        Assert.Equal((1, 2, 3), (character.X, character.Y, character.Z));

        character.Z = -5;

        Assert.Equal(new Point3D(1, 2, -5), character.Location);
    }

    [Fact]
    public async Task Slot_IsStored_AndTwoCharactersOfOneAccountCannotShareIt()
    {
        await using var host = await HostPersistenceFixture.CreateAsync(false);
        host.Container.AddPersistenceWorld<MobileEntity>();
        await CoreMigrationFiles.ApplyAsync(host.Database, "world");
        await host.Owner.InitializeAsync();
        var mobiles = host.Container.Resolve<IDataAccess<MobileEntity>>();
        var account = new Serial(0x42);

        await mobiles.UpsertAsync(new MobileEntity { Name = "Aria", AccountId = account, Slot = 2 });

        Assert.Equal((int?)2, Assert.Single(await mobiles.QueryAsync(mobile => mobile.AccountId == account)).Slot);
        await Assert.ThrowsAnyAsync<Exception>(() =>
            mobiles.UpsertAsync(new MobileEntity { Name = "Bran", AccountId = account, Slot = 2 })
        );
    }

    [Fact]
    public async Task Slot_NpcsWithoutAccountOrSlotNeverCollide()
    {
        await using var host = await HostPersistenceFixture.CreateAsync(false);
        host.Container.AddPersistenceWorld<MobileEntity>();
        await CoreMigrationFiles.ApplyAsync(host.Database, "world");
        await host.Owner.InitializeAsync();
        var mobiles = host.Container.Resolve<IDataAccess<MobileEntity>>();

        await mobiles.UpsertAsync(new MobileEntity { Name = "an orc" });
        await mobiles.UpsertAsync(new MobileEntity { Name = "an orc" });

        Assert.Equal(2, (await mobiles.QueryAsync(mobile => mobile.Name == "an orc")).Count);
    }

    [Fact]
    public async Task DeletionRequestedAt_IsStoredInUtcAndReadBack()
    {
        await using var host = await HostPersistenceFixture.CreateAsync(false);
        host.Container.AddPersistenceWorld<MobileEntity>();
        await CoreMigrationFiles.ApplyAsync(host.Database, "world");
        await host.Owner.InitializeAsync();
        var mobiles = host.Container.Resolve<IDataAccess<MobileEntity>>();
        var requested = new DateTime(2026, 9, 28, 10, 30, 0, DateTimeKind.Utc);
        var aria = new MobileEntity { Name = "Aria", AccountId = new Serial(0x42), Slot = 0, DeletionRequestedAt = requested };

        await mobiles.UpsertAsync(aria);

        var stored = (await mobiles.GetByIdAsync(aria.Id))!;
        Assert.Equal(requested, stored.DeletionRequestedAt);
        Assert.Equal(DateTimeKind.Utc, stored.DeletionRequestedAt!.Value.Kind);
    }

    [Fact]
    public async Task Direction_IsStoredAndReadBack()
    {
        await using var host = await HostPersistenceFixture.CreateAsync(false);
        host.Container.AddPersistenceWorld<MobileEntity>();
        await CoreMigrationFiles.ApplyAsync(host.Database, "world");
        await host.Owner.InitializeAsync();
        var mobiles = host.Container.Resolve<IDataAccess<MobileEntity>>();
        var aria = new MobileEntity { Name = "Aria", AccountId = new Serial(0x42), Slot = 0, Direction = DirectionType.West };

        await mobiles.UpsertAsync(aria);

        Assert.Equal(DirectionType.West, (await mobiles.GetByIdAsync(aria.Id))!.Direction);
    }
}
