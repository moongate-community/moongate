using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Persistence.Types.Persistence;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Tests.TestSupport.Persistence;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Integration.Server.Ultima.Entities;

[Collection(PostgresTestCollection.Name)]
public sealed class CharacterEntityPersistenceTests
{
    [Fact]
    public async Task Location_And_Map_AreStoredAsIntegerColumnsAndReadBack()
    {
        await using var database = await new PostgreSqlFixture().CreateDatabaseAsync();
        using var fixture = new DevelopmentMigrationFixture(database.ConnectionString);
        await using var coordinator = fixture.Create(typeof(CharacterEntity));
        await coordinator.InitializeAsync();
        var orm = coordinator.GetDatabase(PersistenceDatabaseTarget.Realm).Orm;
        var character = new CharacterEntity
        {
            Id = new(1),
            AccountId = new(42),
            Name = "Aria",
            Location = new(738, 3486, -19),
            Map = MapType.TerMur
        };

        await orm.Insert(character).ExecuteAffrowsAsync();
        var loaded = await orm.Select<CharacterEntity>().Where(entity => entity.Name == "Aria").FirstAsync();

        Assert.NotNull(loaded);
        Assert.Equal(new Point3D(738, 3486, -19), loaded.Location);
        Assert.Equal(MapType.TerMur, loaded.Map);
        Assert.Equal(5, await database.ScalarAsync<int>("SELECT map::int FROM world.characters"));
        Assert.Equal(-19, await database.ScalarAsync<int>("SELECT z FROM world.characters"));

        foreach (var column in new[] { "x", "y", "z", "map" })
        {
            Assert.Contains(
                await database.ScalarAsync<string>(
                    "SELECT data_type FROM information_schema.columns WHERE table_schema = 'world' " +
                    $"AND table_name = 'characters' AND column_name = '{column}'"
                ),
                new[] { "integer", "smallint" }
            );
        }

        Assert.Equal(
            0L,
            await database.ScalarAsync<long>(
                "SELECT count(*) FROM information_schema.columns WHERE table_schema = 'world' " +
                "AND table_name = 'characters' AND column_name = 'location'"
            )
        );
    }

    [Fact]
    public async Task CreationFields_RoundTrip_WithHuesAsIntegersAndEnumsAsSmallints()
    {
        await using var database = await new PostgreSqlFixture().CreateDatabaseAsync();
        using var fixture = new DevelopmentMigrationFixture(database.ConnectionString);
        await using var coordinator = fixture.Create(typeof(CharacterEntity));
        await coordinator.InitializeAsync();
        var orm = coordinator.GetDatabase(PersistenceDatabaseTarget.Realm).Orm;
        var createdAt = new DateTime(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc);
        var character = new CharacterEntity
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
        var loaded = await orm.Select<CharacterEntity>().Where(entity => entity.Name == "Aria").FirstAsync();

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
        Assert.Equal(0x83EA, await database.ScalarAsync<int>("SELECT skin_hue FROM world.characters"));
        Assert.Equal(
            "integer",
            await database.ScalarAsync<string>(
                "SELECT data_type FROM information_schema.columns WHERE table_schema = 'world' " +
                "AND table_name = 'characters' AND column_name = 'hair_hue'"
            )
        );
        Assert.Equal(
            "smallint",
            await database.ScalarAsync<string>(
                "SELECT data_type FROM information_schema.columns WHERE table_schema = 'world' " +
                "AND table_name = 'characters' AND column_name = 'race'"
            )
        );
    }

    [Fact]
    public void Location_SetsAndReadsTheThreeCoordinates()
    {
        var character = new CharacterEntity { Location = new(1, 2, 3) };

        Assert.Equal((1, 2, 3), (character.X, character.Y, character.Z));

        character.Z = -5;

        Assert.Equal(new Point3D(1, 2, -5), character.Location);
    }
}
