using Moongate.Server.Ultima.Types.Speech;
using Moongate.Server.Ultima.Data.Regions;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Characters;
using Moongate.Server.Ultima.Data.Events;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Characters;

public sealed class CharacterScriptEventsTests
{
    [Fact]
    public void CharacterCreated_MapsTheFieldsScriptsSee()
    {
        var character = new MobileEntity
        {
            Id = new(0x1234), AccountId = new Serial(42), Name = "Aria", Race = RaceType.Elf, Gender = GenderType.Female,
            Location = new Point3D(4408, 1168, 5), Map = MapType.Trammel
        };

        var fields = CharacterScriptEvents.CharacterCreated(new CharacterCreatedEvent(character, []));

        Assert.Equal(
            new Dictionary<string, object?>
            {
                ["serial"] = 0x1234L, ["account_id"] = 42L, ["name"] = "Aria", ["race"] = RaceType.Elf,
                ["gender"] = GenderType.Female, ["map"] = MapType.Trammel, ["x"] = 4408, ["y"] = 1168, ["z"] = 5
            },
            fields
        );
    }

    [Fact]
    public void CharacterDeletionRequested_MapsSerialAccountAndName()
    {
        var character = new MobileEntity { Id = new(0x0003), AccountId = new Serial(42), Name = "Bran" };

        var fields = CharacterScriptEvents.CharacterDeletionRequested(new CharacterDeletionRequestedEvent(character));

        Assert.Equal(
            new Dictionary<string, object?> { ["serial"] = 3L, ["account_id"] = 42L, ["name"] = "Bran" },
            fields
        );
    }

    [Fact]
    public void CharacterEnteredWorld_MapsSerialAccountNameAndLocation()
    {
        var character = new MobileEntity
        {
            Id = new(0x0002), AccountId = new Serial(42), Name = "Aria", Map = MapType.Trammel,
            Location = new Point3D(1496, 1628, 10)
        };

        var fields = CharacterScriptEvents.CharacterEnteredWorld(new CharacterEnteredWorldEvent(character));

        Assert.Equal(
            new Dictionary<string, object?>
            {
                ["serial"] = 2L, ["account_id"] = 42L, ["name"] = "Aria", ["map"] = MapType.Trammel, ["x"] = 1496,
                ["y"] = 1628, ["z"] = 10
            },
            fields
        );
    }

    [Fact]
    public void PlayerSay_MapsSerialNameTextAndType()
    {
        var character = new MobileEntity { Id = new(0x0002), AccountId = new Serial(42), Name = "Aria" };

        var fields = CharacterScriptEvents.PlayerSay(new PlayerSaidEvent(character, "hello there", SpeechType.Yell));

        Assert.Equal(
            new Dictionary<string, object?> { ["serial"] = 2L, ["name"] = "Aria", ["text"] = "hello there", ["type"] = 9L },
            fields
        );
    }

    [Fact]
    public void CharacterLeftWorld_MapsSerialAccountNameAndLocation()
    {
        var character = new MobileEntity
        {
            Id = new(0x00000002), AccountId = new Serial(0x2A), Name = "Aria", Map = MapType.Trammel,
            Location = new Point3D(1497, 1628, 12)
        };

        var fields = CharacterScriptEvents.CharacterLeftWorld(new CharacterLeftWorldEvent(character));

        Assert.Equal(2L, fields["serial"]);
        Assert.Equal(0x2AL, fields["account_id"]);
        Assert.Equal("Aria", fields["name"]);
        Assert.Equal(MapType.Trammel, fields["map"]);
        Assert.Equal((1497, 1628, 12), ((int)fields["x"]!, (int)fields["y"]!, (int)fields["z"]!));
    }

    [Fact]
    public void PlayerRegionChanged_NamesBothRegions_AndWhereThePlayerStands()
    {
        var aria = new MobileEntity
        {
            Id = new Serial(2), Name = "Aria", Map = MapType.Trammel, Location = new Point3D(1496, 1628, 10)
        };

        var fields = CharacterScriptEvents.PlayerRegionChanged(
            new PlayerRegionChangedEvent(aria, null, new RegionContent { Map = MapType.Trammel, Name = "Britain" })
        );

        Assert.Equal(2L, fields["serial"]);
        Assert.Equal("Aria", fields["name"]);
        Assert.Null(fields["previous"]);
        Assert.Equal("Britain", fields["current"]);
        Assert.Equal((MapType.Trammel, 1496, 1628, 10), (fields["map"], fields["x"], fields["y"], fields["z"]));
    }
}
