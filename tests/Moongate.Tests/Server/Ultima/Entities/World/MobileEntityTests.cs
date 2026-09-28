using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Core.Types.Geometry;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Types.Mobiles;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Entities.World;

public sealed class MobileEntityTests
{
    [Fact]
    public void ToString_PlayerCharacter_ShowsItsAccountRaceGenderBodyAndLocation()
    {
        var where = new Point3D(4408, 1168, 5);
        var aria = new MobileEntity
        {
            Id = new(0x00000002), AccountId = new Serial(0x2A), Name = "Aria", Race = RaceType.Elf,
            Gender = GenderType.Female, Body = 0x025E, Map = MapType.Trammel, Location = where
        };

        Assert.Equal($"0x00000002 \"Aria\" player of 0x0000002A (Elf Female, body 0x025E) at Trammel {where}", aria.ToString());
    }

    [Fact]
    public void ToString_Npc_ShowsItsTemplate()
    {
        var where = new Point3D(1496, 1628, 10);
        var orc = new MobileEntity
        {
            Id = new(0x00000005), TemplateId = "orc", Name = "an orc", Body = 0x0011, Map = MapType.Felucca,
            Location = where
        };

        Assert.Equal($"0x00000005 \"an orc\" npc \"orc\" (Human Male, body 0x0011) at Felucca {where}", orc.ToString());
    }

    [Fact]
    public void Props_SetGetConvertAndRemove()
    {
        var mobile = new MobileEntity();

        mobile.SetProp("quest_step", 3);
        mobile.SetProp("title_color", NotorietyType.Criminal);

        Assert.Equal(3, mobile.GetProp<int>("quest_step"));
        Assert.Equal(NotorietyType.Criminal, mobile.GetProp<NotorietyType>("title_color"));
        Assert.Equal("none", mobile.GetProp("missing", "none"));
        Assert.True(mobile.RemoveProp("quest_step"));
        mobile.SetProp("title_color", null);
        Assert.Null(mobile.Props);
    }

    [Fact]
    public void Props_ANestedValue_IsRejected_AndABadConversionThrows()
    {
        var mobile = new MobileEntity { Props = new() { ["hits"] = "many" } };

        Assert.Throws<ArgumentException>(() => mobile.SetProp("list", new List<int>()));
        Assert.Contains("'hits'", Assert.Throws<InvalidCastException>(() => mobile.GetProp<int>("hits")).Message);
    }

    [Fact]
    public void Direction_DefaultsToSouth()
    {
        Assert.Equal(DirectionType.South, new MobileEntity().Direction);
    }

    [Fact]
    public void Snapshot_IsADetachedCopyWithTheSameValues()
    {
        var aria = new MobileEntity
        {
            Id = new(0x00000002), AccountId = new Serial(0x2A), Name = "Aria", Map = MapType.Trammel,
            Location = new Point3D(1496, 1628, 10), Direction = DirectionType.East,
            Skills = [new() { Skill = SkillType.Magery, Base = 500 }]
        };
        aria.SetProp("quest", 3);

        var snapshot = aria.Snapshot();
        aria.Location = new Point3D(1, 2, 3);
        aria.Skills[0].Base = 900;
        aria.SetProp("quest", 4);

        Assert.NotSame(aria, snapshot);
        Assert.Equal(
            (new Serial(0x00000002), "Aria", new Point3D(1496, 1628, 10), DirectionType.East, 500, 3),
            (snapshot.Id, snapshot.Name, snapshot.Location, snapshot.Direction, snapshot.Skills[0].Base,
                snapshot.GetProp<int>("quest"))
        );
    }
}
