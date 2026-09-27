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
}
