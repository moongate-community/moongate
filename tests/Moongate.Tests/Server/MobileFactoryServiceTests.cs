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

        // Body derives from race + gender (elf female); pools seed from the stats, topped up.
        Assert.Equal(0x25E, character.Body);
        Assert.Equal(72, character.HitsMax); // players: 50 + Str/2 == 50 + 45/2
        Assert.Equal(72, character.Hits);
        Assert.Equal(20, character.StaminaMax);
        Assert.Equal(20, character.Stamina);
        Assert.Equal(25, character.ManaMax);
        Assert.Equal(25, character.Mana);

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

    [Theory]
    // Sum is not the 90-point budget.
    [InlineData(60, 60, 60)]
    [InlineData(10, 10, 10)]
    // Sums to 90, but a single stat escapes the [10, 60] range.
    [InlineData(70, 10, 10)]
    [InlineData(80, 5, 5)]
    public void CreatePlayerMobile_InvalidStartingStats_FloorsEveryStat(byte strength, byte dexterity, byte intelligence)
    {
        var character = Factory().CreatePlayerMobile(Packet(1, strength, dexterity, intelligence));

        Assert.Equal(10, character.Strength);
        Assert.Equal(10, character.Dexterity);
        Assert.Equal(10, character.Intelligence);

        // Pools follow the floored stats: 50 + 10/2 == 55 hits.
        Assert.Equal(55, character.HitsMax);
        Assert.Equal(55, character.Hits);
        Assert.Equal(10, character.StaminaMax);
        Assert.Equal(10, character.ManaMax);
    }

    [Theory]
    // Every stat inside [10, 60] and summing to the 90-point budget.
    [InlineData(60, 20, 10, 80)]
    [InlineData(10, 20, 60, 55)]
    [InlineData(30, 30, 30, 65)]
    public void CreatePlayerMobile_ValidStartingStats_AreKept(
        byte strength,
        byte dexterity,
        byte intelligence,
        int expectedHits
    )
    {
        var character = Factory().CreatePlayerMobile(Packet(1, strength, dexterity, intelligence));

        Assert.Equal(strength, character.Strength);
        Assert.Equal(dexterity, character.Dexterity);
        Assert.Equal(intelligence, character.Intelligence);
        Assert.Equal(expectedHits, character.HitsMax);
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

        // Creature pools mirror the raw stats (no flat hit-point base) and start topped up.
        Assert.Equal(100, spawn.Mobile.HitsMax);
        Assert.Equal(100, spawn.Mobile.Hits);
        Assert.Equal(90, spawn.Mobile.StaminaMax);
        Assert.Equal(90, spawn.Mobile.Stamina);
        Assert.Equal(25, spawn.Mobile.ManaMax);
        Assert.Equal(25, spawn.Mobile.Mana);

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
    public void CreateFromTemplate_VariantGender_KeepsGenderBodyAndEquipmentCoherent()
    {
        var templates = new MobileTemplateService();
        templates.Register(
            new()
            {
                Id = "guard",
                Name = "Guard",
                Variants =
                [
                    new()
                    {
                        Name = "m",
                        Gender = MobileTemplateGenderType.Male,
                        Appearance = new() { Body = 400 },
                        Equipment = [new() { Item = "male_plate", Layer = "InnerTorso" }]
                    },
                    new()
                    {
                        Name = "f",
                        Gender = MobileTemplateGenderType.Female,
                        Appearance = new() { Body = 401 },
                        Equipment = [new() { Item = "female_plate", Layer = "InnerTorso" }]
                    }
                ]
            }
        );
        var factory = Factory(templates);

        var combos = new HashSet<(GenderType, int, string)>();

        for (var i = 0; i < 40; i++)
        {
            var spawn = factory.CreateFromTemplate("guard", 1, new(0, 0, 0))!;
            combos.Add((spawn.Mobile.Gender, spawn.Mobile.Body, Assert.Single(spawn.Equipment).ItemTemplateId));
        }

        // Both coherent packages appear and nothing else — never a mismatched gender/body/gear.
        Assert.Contains((GenderType.Male, 400, "male_plate"), combos);
        Assert.Contains((GenderType.Female, 401, "female_plate"), combos);
        Assert.Equal(2, combos.Count);
    }

    [Fact]
    public void CreateFromTemplate_VariantLoot_OverridesTemplateOtherwiseInherits()
    {
        var templates = new MobileTemplateService();
        templates.Register(
            new()
            {
                Id = "guard",
                Name = "Guard",
                LootTableId = "guard.base",
                Variants =
                [
                    new() { Name = "rich", LootTableId = "guard.rich" },
                    new() { Name = "plain" }
                ]
            }
        );
        var factory = Factory(templates);

        var loots = Enumerable.Range(0, 40)
            .Select(_ => factory.CreateFromTemplate("guard", 1, new(0, 0, 0))!.Mobile.LootTableId)
            .ToHashSet();

        Assert.Contains("guard.rich", loots); // variant override wins
        Assert.Contains("guard.base", loots); // variant without loot inherits the template
        Assert.Equal(2, loots.Count);
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

    private static CharacterCreationPacket Packet(
        short startingCityIndex,
        byte strength = 45,
        byte dexterity = 20,
        byte intelligence = 25
    )
        => new(
            0,
            "Freydis",
            0,
            4,
            GenderType.Female,
            RaceType.Elf,
            strength,
            dexterity,
            intelligence,
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
