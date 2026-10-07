using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Data.Names;
using Moongate.Server.Ultima.Data.Races;
using Moongate.Server.Ultima.Data.Templates.Mobiles;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Services.Internal;
using Moongate.Server.Ultima.Types.Mobiles;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class MobileFactoryServiceTests
{
    private readonly MobileFactoryService _factory;

    public MobileFactoryServiceTests()
    {
        var loaders = new StubDataLoaderService()
            .With(
                new MobileTemplate
                {
                    Id = "guard", Race = RaceType.Human, Gender = MobileGenderType.Random, NameList = "{gender}",
                    Strength = DiceSpec.Parse("1d10+90"), Dexterity = DiceSpec.FromValue(80),
                    Intelligence = DiceSpec.FromValue(70), Mana = DiceSpec.FromValue(5),
                    Resistances = new MobileResistances { Fire = DiceSpec.FromValue(30) },
                    Skills = new() { ["tactics"] = DiceSpec.FromValue(95) },
                    Fame = DiceSpec.FromValue(500), Karma = DiceSpec.FromValue(-100), Armor = DiceSpec.FromValue(20)
                },
                new MobileTemplate
                {
                    Id = "bald_monk", Race = RaceType.Human, Gender = MobileGenderType.Male, Name = "a monk", Hair = []
                },
                new MobileTemplate { Id = "orc", Body = 17, NameList = "orc" },
                new MobileTemplate { Id = "red_orc", Body = 17, NameList = "orc", Notoriety = NotorietyType.Murderer },
                new MobileTemplate
                {
                    Id = "banker", Race = RaceType.Human, Name = "a banker", Notoriety = NotorietyType.Invulnerable
                },
                new MobileTemplate { Id = "lord", Race = RaceType.Human, Name = "Lord British" }
            )
            .With(
                new RaceContent
                {
                    Race = RaceType.Human, Name = "Human",
                    SkinHues = [HueSpec.FromRange(0x3EA, 0x422)], HairHues = [HueSpec.FromRange(0x44E, 0x47D)],
                    Male = new RaceGenderContent { Body = 400, Hair = [0x203B], Beard = [0x203E] },
                    Female = new RaceGenderContent { Body = 401, Hair = [0x203C], Beard = [] }
                }
            )
            .With(
                new NameList { Id = "male", Names = ["Aaron"] },
                new NameList { Id = "female", Names = ["Alice"] },
                new NameList { Id = "orc", Names = ["Grok"] }
            );

        // Create touches no persistence, map, items or events; the integration tests cover SpawnAsync.
        _factory = new MobileFactoryService(
            new MobileTemplateService(loaders),
            new NameService(loaders),
            loaders,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!
        );
    }

    [Fact]
    public void Create_ARandomGenderHuman_GetsTheBodyNameAndLooksOfTheRolledGender()
    {
        var genders = new HashSet<GenderType>();

        for (var i = 0; i < 200; i++)
        {
            var guard = _factory.Create("guard");
            genders.Add(guard.Gender);
            var male = guard.Gender == GenderType.Male;

            Assert.Equal((male ? 400 : 401, male ? "Aaron" : "Alice"), (guard.Body, guard.Name));
            Assert.Equal(male ? 0x203B : 0x203C, guard.HairStyle);
            Assert.Equal(male ? 0x203E : 0, guard.BeardStyle);
            Assert.InRange(guard.SkinHue.Value, 0x3EA, 0x422);
            Assert.InRange(guard.HairHue.Value, 0x44E, 0x47D);
        }

        Assert.Equal(2, genders.Count);
    }

    [Fact]
    public void Create_RollsStatsOnce_WithHitsAndStaminaFromStrAndDexWhenUnset()
    {
        var guard = _factory.Create("guard");

        Assert.Equal(("guard", Serial.Zero, (Serial?)null), (guard.TemplateId, guard.Id, guard.AccountId));
        Assert.InRange(guard.Strength, 91, 100);
        Assert.Equal(
            (guard.Strength, guard.Strength, 80, 80, 5, 5),
            (guard.Hits, guard.HitsMax, guard.Stamina, guard.StaminaMax, guard.Mana, guard.ManaMax)
        );
        Assert.Equal((500, -100, 20, 30, 0), (guard.Fame, guard.Karma, guard.Armor, guard.ResistFire, guard.ResistCold));
        var tactics = Assert.Single(guard.Skills);
        Assert.Equal((SkillType.Tactics, 950), (tactics.Skill, tactics.Base));
        Assert.Null(guard.Title);
        // A human that the template gives no notoriety is innocent, which is what no notoriety reads as.
        Assert.Null(guard.Notoriety);
    }

    [Fact]
    public void Create_GivesTheNpcTheNotorietyOfItsTemplate()
    {
        Assert.Equal(NotorietyType.Murderer, _factory.Create("red_orc").Notoriety);
        Assert.Equal(NotorietyType.Invulnerable, _factory.Create("banker").Notoriety);
    }

    [Fact]
    public void Create_ANonHumanWithoutANotoriety_IsAttackable_AsAnimalsAndMonstersAreInModernUO()
    {
        Assert.Equal(NotorietyType.Attackable, _factory.Create("orc").Notoriety);
    }

    [Fact]
    public void NpcNotoriety_OfAHumanWithoutOne_IsNone_AndOfNoTemplate_IsNone()
    {
        Assert.Null(NpcNotoriety.Of(new MobileTemplate { Id = "lord" }, 0x190));
        Assert.Null(NpcNotoriety.Of(null, 17));
        Assert.Equal(NotorietyType.Attackable, NpcNotoriety.Of(new MobileTemplate { Id = "cat" }, 201));
        Assert.Equal(
            NotorietyType.Enemy,
            NpcNotoriety.Of(new MobileTemplate { Id = "cat", Notoriety = NotorietyType.Enemy }, 201)
        );
    }

    [Fact]
    public void Create_AnEmptyHairList_IsBald_AndABodyTemplateHasNoRaceLooks()
    {
        var monk = _factory.Create("bald_monk");
        var orc = _factory.Create("orc");

        Assert.Equal(("a monk", 0), (monk.Name, monk.HairStyle));
        Assert.Equal((17, "Grok", 0, 0, (ushort)0), (orc.Body, orc.Name, orc.HairStyle, orc.BeardStyle, orc.SkinHue.Value));
        Assert.Equal(RaceType.Human, orc.Race);
    }

    [Fact]
    public void Create_UnsetGenderIsMale_AndUnsetStatsAreTen_AsTheTemplateContractSays()
    {
        Assert.All(Enumerable.Range(0, 50), _ => Assert.Equal(GenderType.Male, _factory.Create("lord").Gender));
        var lord = _factory.Create("lord");
        Assert.Equal(
            (10, 10, 10, 10, 10, 10),
            (lord.Strength, lord.Dexterity, lord.Intelligence, lord.HitsMax, lord.StaminaMax, lord.ManaMax)
        );
    }

    [Fact]
    public void Create_AnUnknownTemplate_Throws()
    {
        Assert.Contains("'ettin'", Assert.Throws<KeyNotFoundException>(() => _factory.Create("ettin")).Message);
    }
}
