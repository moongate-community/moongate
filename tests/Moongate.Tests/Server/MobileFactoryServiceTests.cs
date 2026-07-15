using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Network.Packets.Incoming;
using Moongate.Server.Services.Mobiles;
using Moongate.Server.Services.World;
using Moongate.UO.Data.Mobiles.Templates;
using Moongate.UO.Data.Types;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server;

public class MobileFactoryServiceTests
{
    [Fact]
    public void CreatePlayerMobile_MapsIdentityStatsAppearanceHuesSkillsAndStartingLocation()
    {
        var character = Factory().CreatePlayerMobile(Packet(1));

        Assert.Equal("Freydis", character.Name);
        Assert.Equal(GenderType.Female, character.Gender);
        Assert.Equal(RaceType.Elf, character.Race);
        Assert.Equal((byte)4, character.ProfessionId);
        Assert.Equal(45, character.Strength);
        Assert.Equal(20, character.Dexterity);
        Assert.Equal(25, character.Intelligence);
        Assert.Equal((ushort)0x03EA, character.SkinHue.Value);
        Assert.Equal((ushort)0x203C, character.HairStyle);
        Assert.Equal((ushort)0x044E, character.HairHue.Value);
        Assert.Equal((ushort)0x2040, character.FacialHairStyle);
        Assert.Equal((ushort)0x0450, character.FacialHairHue.Value);

        // Four skill slots: three chosen (stored in tenths) and one unused (0,0) skipped.
        Assert.Equal(3, character.Skills.Count);
        Assert.Equal(500, character.Skills[1]);
        Assert.Equal(300, character.Skills[2]);
        Assert.Equal(200, character.Skills[3]);
        Assert.False(character.Skills.ContainsKey(0));

        // Starting location taken from the city at index 1 (Moonglow / Felucca).
        Assert.Equal((int)MapType.Felucca, character.MapId);
        Assert.Equal(new(4408, 1168, 0), character.Position);
    }

    [Fact]
    public void CreatePlayerMobile_OutOfRangeCityIndex_FallsBackToFirstCity()
    {
        var character = Factory().CreatePlayerMobile(Packet(99));

        // Falls back to the city at index 0 (Britain / Trammel).
        Assert.Equal((int)MapType.Trammel, character.MapId);
        Assert.Equal(new(1602, 1591, 20), character.Position);
    }

    [Fact]
    public void Create_BuildsBareMobileWithNameMapAndPosition()
    {
        var mobile = Factory().Create("Town Guard", 1, new(1420, 1690, 5));

        Assert.Equal("Town Guard", mobile.Name);
        Assert.Equal(1, mobile.MapId);
        Assert.Equal(new(1420, 1690, 5), mobile.Position);
        Assert.Equal(Serial.Zero, mobile.Id);
    }

    [Fact]
    public void CreateFromTemplate_UnknownId_ReturnsNull()
        => Assert.Null(Factory().CreateFromTemplate("nope", 1, new(0, 0, 0)));

    [Fact]
    public void CreateFromTemplate_AppliesBodyStatsHuesAndSkills()
    {
        var templates = new MobileTemplateService();
        templates.Register(
            new()
            {
                Id = "guard",
                Name = "Town Guard",
                Strength = 100,
                Dexterity = 90,
                Intelligence = 25,
                BrainScript = "guard",
                LootTableId = "guard.warrior",
                Appearance = new() { Body = 400, SkinHue = "1002" },
                Skills = { ["Swordsmanship"] = 900, ["Bogus"] = 10 }
            }
        );

        var spawn = Factory(templates).CreateFromTemplate("guard", 1, new(10, 20, 5))!;

        Assert.Equal("Town Guard", spawn.Mobile.Name);
        Assert.Equal(400, spawn.Mobile.Body);
        Assert.Equal(100, spawn.Mobile.Strength);
        Assert.Equal((ushort)1002, spawn.Mobile.SkinHue.Value);
        Assert.Equal(new(10, 20, 5), spawn.Mobile.Position);
        Assert.Equal("guard", spawn.Mobile.BrainScriptId);
        Assert.Equal("guard.warrior", spawn.Mobile.LootTableId);
        // 40 == Swordsmanship; the unknown "Bogus" skill is skipped.
        Assert.Equal(900, spawn.Mobile.Skills[40]);
        Assert.DoesNotContain(spawn.Mobile.Skills, pair => pair.Value == 10);
    }

    [Fact]
    public void CreateFromTemplate_AppliesGender()
    {
        var templates = new MobileTemplateService();
        templates.Register(new() { Id = "male", Name = "Guard" });
        templates.Register(new() { Id = "female", Name = "Guard", Gender = MobileTemplateGenderType.Female });
        var factory = Factory(templates);

        Assert.Equal(GenderType.Male, factory.CreateFromTemplate("male", 1, new(0, 0, 0))!.Mobile.Gender);
        Assert.Equal(GenderType.Female, factory.CreateFromTemplate("female", 1, new(0, 0, 0))!.Mobile.Gender);
    }

    [Fact]
    public void CreateFromTemplate_RandomGender_RollsBothValues()
    {
        var templates = new MobileTemplateService();
        templates.Register(new() { Id = "any", Name = "Guard", Gender = MobileTemplateGenderType.Random });
        var factory = Factory(templates);

        var genders = Enumerable.Range(0, 40)
                                .Select(_ => factory.CreateFromTemplate("any", 1, new(0, 0, 0))!.Mobile.Gender)
                                .ToHashSet();

        Assert.Contains(GenderType.Male, genders);
        Assert.Contains(GenderType.Female, genders);
    }

    [Fact]
    public void CreateFromTemplate_ResolvesEquipment()
    {
        var templates = new MobileTemplateService();
        templates.Register(
            new()
            {
                Id = "guard",
                Name = "Guard",
                Appearance = new() { Body = 400 },
                Equipment = [new() { Item = "plate_chest", Layer = "InnerTorso", Hue = "1002" }]
            }
        );

        var spawn = Factory(templates).CreateFromTemplate("guard", 1, new(0, 0, 0))!;

        var equip = Assert.Single(spawn.Equipment);
        Assert.Equal("plate_chest", equip.ItemTemplateId);
        Assert.Equal(LayerType.InnerTorso, equip.Layer);
        Assert.Equal((ushort)1002, equip.Hue);
    }

    private static MobileFactoryService Factory(MobileTemplateService? templates = null)
        => new(Cities(), templates ?? new MobileTemplateService(), new Random(1));

    private static StartingCityService Cities()
    {
        var service = new StartingCityService();

        // index 0
        service.Register(
            new()
            {
                City = "Britain", Building = "Inn", Description = 1, X = 1602, Y = 1591, Z = 20, Map = MapType.Trammel
            }
        );

        // index 1
        service.Register(
            new()
            {
                City = "Moonglow", Building = "Inn", Description = 2, X = 4408, Y = 1168, Z = 0, Map = MapType.Felucca
            }
        );

        return service;
    }

    private static CharacterCreationPacket Packet(short startingCityIndex)
        => new(
            0,
            "Freydis",
            0,
            4,
            GenderType.Female,
            RaceType.Elf,
            45,
            20,
            25,
            [new(1, 50), new(2, 30), new(3, 20), new(0, 0)],
            0x03EA,
            0x203C,
            0x044E,
            0x2040,
            0x0450,
            startingCityIndex,
            0x0765,
            0x0766
        );
}
