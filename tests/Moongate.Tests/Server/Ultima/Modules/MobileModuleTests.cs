using Moongate.Tests.TestSupport.Ultima.Weight;
using Lua;
using Lua.Standard;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Scripting.Binding;
using Moongate.Scripting.Internal;
using Moongate.Scripting.Utils;
using Moongate.Core.Types.Geometry;
using Moongate.Server.Ultima.Data.Regions;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.World;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Modules;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Ultima.Death;
using Moongate.Tests.TestSupport.Ultima.Mobiles;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Tests.TestSupport.Ultima.Sectors;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Server.Ultima.Data.Bodies;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Modules;

public sealed class MobileModuleTests
{
    private readonly RecordingCrimeService _crimes = new();
    private readonly StubDeathService _death = new();
    private readonly RecordingSpeechService _speech = new();
    private readonly RecordingTeleportService _teleports = new();
    private readonly RecordingMobileStateService _state = new();
    private readonly RecordingWorldViewService _view = new();
    private readonly MobileService _mobiles = new(new StubMovementService(), TestSectors.Create());
    private readonly ItemService _items = TestItems.Create();
    private readonly StubMusicService _music = new();
    private readonly RecordingLightService _light = new();
    private readonly RegionService _regions = new(
        new StubDataLoaderService().With(
            new RegionContent
            {
                Map = MapType.Felucca, Name = "Britain", Areas = [new RegionAreaContent { X1 = 1400, Y1 = 1500, X2 = 1700, Y2 = 1800 }]
            }
        )
    );
    private readonly MobileEntity _orc = new()
    {
        Id = new Serial(0x100), Name = "an orc", TemplateId = "orc", Map = MapType.Felucca, Location = new Point3D(3000, 3000, 0),
        Body = 17, Strength = 96, Hits = 50, HitsMax = 58, Direction = DirectionType.West
    };
    private readonly MobileEntity _aria = new()
    {
        Id = new Serial(2), Name = "Aria", AccountId = new Serial(0x42), Map = MapType.Felucca,
        Location = new Point3D(1601, 1600, 5)
    };

    public MobileModuleTests()
    {
        _mobiles.EnterWorld(_aria);
        _mobiles.EnterWorld(_orc);
        var backpack = new ItemEntity { Id = new Serial(0x40000001), TemplateId = "backpack", ItemId = 0x0E75, Amount = 1 };
        backpack.Equip(_aria.Id, LayerType.Backpack);
        _items.Add([backpack]);
    }

    [Fact]
    public void NameIsPlayerAndDirection_DescribeTheMobile()
    {
        var result = Run(
            "return mobile.name(2), mobile.is_player(2), mobile.is_player(256), mobile.direction(256), mobile.name(999), mobile.is_player(999), mobile.direction(999)"
        );

        Assert.Equal("Aria", result[0].Read<string>());
        Assert.True(result[1].Read<bool>());
        Assert.False(result[2].Read<bool>());
        Assert.Equal((int)DirectionType.West, result[3].Read<int>());
        Assert.Equal(LuaValue.Nil, result[4]);
        Assert.False(result[5].Read<bool>());
        Assert.Equal(LuaValue.Nil, result[6]);
    }

    [Fact]
    public void Stats_GivesTheMobilesNumbers()
    {
        var result = Run("local stats = mobile.stats(256) return stats.body, stats.strength, stats.hits, stats.hits_max, mobile.stats(999)");

        Assert.Equal([17, 96, 50, 58], result[..4].Select(value => value.Read<int>()));
        Assert.Equal(LuaValue.Nil, result[4]);
    }

    [Fact]
    public void BackpackRegionAndLight_ComeFromTheWorld()
    {
        var result = Run(
            "return mobile.backpack(2), mobile.backpack(256), mobile.region(2), mobile.region(256), mobile.light(2), mobile.light(999)"
        );

        Assert.Equal(0x40000001, result[0].Read<long>());
        Assert.Equal(LuaValue.Nil, result[1]);
        Assert.Equal("Britain", result[2].Read<string>());
        Assert.Equal(LuaValue.Nil, result[3]);
        Assert.Equal(_light.LevelFor(_aria), result[4].Read<int>());
        Assert.Equal(LuaValue.Nil, result[5]);
    }

    [Fact]
    public void PlayMusic_PlaysItToAPlayerOnly()
    {
        var result = Run("return mobile.play_music(2, 'Britain1'), mobile.play_music(256, 'Britain1'), mobile.play_music(999, 'Britain1')");

        Assert.True(result[0].Read<bool>());
        Assert.False(result[1].Read<bool>());
        Assert.False(result[2].Read<bool>());
    }

    [Fact]
    public void Props_AreKeptOnPlayersAndNpcs()
    {
        var result = Run(
            "return mobile.set_prop(2, 'quest.step', 2), mobile.get_prop(2, 'quest.step'), mobile.set_prop(256, 'angry', true), " +
            "mobile.get_prop(256, 'angry'), mobile.get_prop(2, 'nothing')"
        );

        Assert.True(result[0].Read<bool>());
        Assert.Equal(2, result[1].Read<int>());
        Assert.True(result[2].Read<bool>());
        Assert.True(result[3].Read<bool>());
        Assert.Equal(LuaValue.Nil, result[4]);
        Assert.Equal(2L, _aria.Props!["quest.step"]);
    }

    [Fact]
    public void SetProp_WithNil_RemovesIt()
    {
        var result = Run("mobile.set_prop(2, 'quest.step', 2) return mobile.set_prop(2, 'quest.step'), mobile.get_prop(2, 'quest.step')");

        Assert.True(result[0].Read<bool>());
        Assert.Equal(LuaValue.Nil, result[1]);
    }

    [Theory,
     InlineData("return mobile.set_prop(999, 'a', 1)"),
     InlineData("return mobile.set_prop(2, ' ', 1)"),
     InlineData("return mobile.set_prop(2, 'a', {})")]
    public void SetProp_WhatCannotBeKept_IsFalse(string chunk)
    {
        Assert.False(Run(chunk)[0].Read<bool>());
    }

    [Fact]
    public void Teleport_AMobileInTheWorld_AsksForTheTeleport()
    {
        var result = Run("return mobile.teleport(2, 5690, 569, 25)");

        Assert.True(result[0].Read<bool>());
        Assert.Equal((_aria, MapType.Felucca, new Point3D(5690, 569, 25)), Assert.Single(_teleports.Teleports));
    }

    [Fact]
    public void Teleport_WithAMap_AsksForTheTeleportToThatMap()
    {
        var result = Run("return mobile.teleport(2, 100, 200, 5, 4)");

        Assert.True(result[0].Read<bool>());
        Assert.Equal((_aria, MapType.Tokuno, new Point3D(100, 200, 5)), Assert.Single(_teleports.Teleports));
    }

    [Theory,
     InlineData("return mobile.teleport(2, 100, 200, 5, 'Tokuno')"),
     InlineData("return mobile.teleport(2, 100, 200, 5, 'tokuno')")]
    public void Teleport_WithAMapName_AsksForTheTeleportToThatMap(string chunk)
    {
        Assert.True(Run(chunk)[0].Read<bool>());

        Assert.Equal((_aria, MapType.Tokuno, new Point3D(100, 200, 5)), Assert.Single(_teleports.Teleports));
    }

    [Fact]
    public void Teleport_WithANilMap_StaysOnTheMobilesMap()
    {
        Assert.True(Run("return mobile.teleport(2, 100, 200, 5, nil)")[0].Read<bool>());

        Assert.Equal((_aria, MapType.Felucca, new Point3D(100, 200, 5)), Assert.Single(_teleports.Teleports));
    }

    [Theory,
     InlineData("return mobile.teleport(2, 100, 200, 5, 6)"),
     InlineData("return mobile.teleport(2, 100, 200, 5, -1)"),
     InlineData("return mobile.teleport(2, 100, 200, 5, 1.5)"),
     InlineData("return mobile.teleport(2, 100, 200, 5, 'Atlantis')"),
     InlineData("return mobile.teleport(2, 100, 200, 5, '4')"),
     InlineData("return mobile.teleport(2, 100, 200, 5, true)")]
    public void Teleport_ToAMapThatDoesNotExist_IsFalseAndAsksNothing(string chunk)
    {
        Assert.False(Run(chunk)[0].Read<bool>());

        Assert.Empty(_teleports.Teleports);
    }

    [Fact]
    public void Teleport_Refused_IsFalse()
    {
        _teleports.Result = false;

        Assert.False(Run("return mobile.teleport(2, 5690, 569, 25)")[0].Read<bool>());
    }

    [Theory,
     InlineData("return mobile.teleport(999, 5690, 569, 25)"),
     InlineData("return mobile.teleport(-1, 5690, 569, 25)"),
     InlineData("return mobile.teleport(2, 5690, 569, 128)"),
     InlineData("return mobile.teleport(2, 5690, 569, -129)")]
    public void Teleport_AnUnknownMobileOrAHeightOutOfRange_IsFalseAndAsksNothing(string chunk)
    {
        Assert.False(Run(chunk)[0].Read<bool>());

        Assert.Empty(_teleports.Teleports);
    }

    [Fact]
    public void Location_GivesWhereTheMobileStands()
    {
        var result = Run("local at = mobile.location(2) return at.x, at.y, at.z, at.map");

        Assert.Equal([1601, 1600, 5, (int)MapType.Felucca], result.Select(value => value.Read<int>()));
    }

    [Fact]
    public void Location_OfAnUnknownMobile_IsNil()
    {
        Assert.Equal(LuaValueType.Nil, Run("return mobile.location(999)")[0].Type);
    }

    [Fact]
    public void PlaySound_PlaysItWhereTheMobileStands()
    {
        var result = Run("return mobile.play_sound(2, 0x1FE)");

        Assert.True(result[0].Read<bool>());
        Assert.Equal((_aria, 0x1FE), Assert.Single(_speech.Sounds));
    }

    [Theory,
     InlineData("return mobile.play_sound(999, 0x1FE)"),
     InlineData("return mobile.play_sound(2, -1)"),
     InlineData("return mobile.play_sound(2, 65536)")]
    public void PlaySound_AnUnknownMobileOrSound_IsFalseAndSilent(string chunk)
    {
        Assert.False(Run(chunk)[0].Read<bool>());

        Assert.Empty(_speech.Sounds);
    }

    [Fact]
    public void Animate_PlaysTheActionOfTheMobile_FiveFramesOnceByDefault()
    {
        var result = Run("return mobile.animate(2, 32), mobile.animate(2, 17, 7, 3)");

        Assert.Equal((true, true), (result[0].Read<bool>(), result[1].Read<bool>()));
        Assert.Equal(["Animated 2 32 5 1", "Animated 2 17 7 3"], _view.Calls);
    }

    [Theory,
     InlineData("return mobile.animate(999, 32)"),
     InlineData("return mobile.animate(2, -1)"),
     InlineData("return mobile.animate(2, 65536)"),
     InlineData("return mobile.animate(2, 32, 0)"),
     InlineData("return mobile.animate(2, 32, 5, 0)"),
     InlineData("return mobile.animate(2, 32, 300)")]
    public void Animate_AnUnknownMobileOrNumbersOutOfRange_IsFalseAndShowsNothing(string chunk)
    {
        Assert.False(Run(chunk)[0].Read<bool>());

        Assert.Empty(_view.Calls);
    }

    [Fact]
    public void Hunger_IsHowFullTheMobileIs_AndSetHungerKeepsItFromZeroToTwenty()
    {
        var result = Run(
            "local full = mobile.hunger(2) " +
            "return full, mobile.set_hunger(2, 7), mobile.hunger(2), mobile.set_hunger(2, 99), mobile.hunger(2), " +
            "mobile.set_hunger(2, -4), mobile.hunger(2), mobile.hunger(999), mobile.set_hunger(999, 5)"
        );

        Assert.Equal([20, 7, 20, 0], new[] { result[0], result[2], result[4], result[6] }.Select(value => value.Read<int>()));
        Assert.All(new[] { result[1], result[3], result[5] }, value => Assert.True(value.Read<bool>()));
        Assert.Equal((LuaValue.Nil, false), (result[7], result[8].Read<bool>()));
        Assert.Equal(0, _aria.Hunger);
    }

    [Fact]
    public void Thirst_IsHowQuenchedTheMobileIs_AndSetThirstKeepsItFromZeroToTwenty()
    {
        var result = Run(
            "local full = mobile.thirst(2) " +
            "return full, mobile.set_thirst(2, 7), mobile.thirst(2), mobile.set_thirst(2, 99), mobile.thirst(2), " +
            "mobile.set_thirst(2, -4), mobile.thirst(2), mobile.thirst(999), mobile.set_thirst(999, 5)"
        );

        Assert.Equal([20, 7, 20, 0], new[] { result[0], result[2], result[4], result[6] }.Select(value => value.Read<int>()));
        Assert.All(new[] { result[1], result[3], result[5] }, value => Assert.True(value.Read<bool>()));
        Assert.Equal((LuaValue.Nil, false), (result[7], result[8].Read<bool>()));
        Assert.Equal((0, 20), (_aria.Thirst, _aria.Hunger));
    }

    [Fact]
    public void Weight_AndMaxWeight_AreTheStonesTheMobileCarriesAndMayCarry()
    {
        var result = Run("return mobile.weight(2), mobile.max_weight(2), mobile.weight(999), mobile.max_weight(999)");

        Assert.Equal((37, 215), (result[0].Read<int>(), result[1].Read<int>()));
        Assert.Equal((LuaValue.Nil, LuaValue.Nil), (result[2], result[3]));
    }

    [Fact]
    public void Kill_HandsTheMobileAndItsKillerToTheDeath_AndGivesItsAnswer()
    {
        var result = Run($"return mobile.kill({_orc.Id.Value}, 2), mobile.kill({_orc.Id.Value}), mobile.kill(999)");

        Assert.Equal([true, true, false], result.Select(value => value.Read<bool>()));
        Assert.Equal([(_orc, (MobileEntity?)_aria), (_orc, null)], _death.Killed);
    }

    [Fact]
    public async Task Resurrect_ACorpse_IsTrue_AndTheRaisingIsStarted()
    {
        var corpse = new ItemEntity { Id = new Serial(0x40000900), TemplateId = "corpse", ItemId = 0x2006, Amount = 1 };
        corpse.PlaceOnGround(MapType.Felucca, new Point3D(3000, 3000, 0));
        _items.Add([corpse]);

        Assert.True(Run("return mobile.resurrect(0x40000900)")[0].Read<bool>());

        var until = DateTime.UtcNow + TimeSpan.FromSeconds(5);

        while (DateTime.UtcNow < until)
        {
            lock (_death.Raised)
            {
                if (_death.Raised.Count > 0)
                {
                    break;
                }
            }

            await Task.Delay(10);
        }

        lock (_death.Raised)
        {
            Assert.Equal([corpse.Id], _death.Raised);
        }
    }

    [Fact]
    public void Resurrect_WhatIsNoCorpse_IsFalse_AndStartsNothing()
    {
        // The backpack of the fixture, a mobile, nothing.
        var result = Run("return mobile.resurrect(0x40000001), mobile.resurrect(2), mobile.resurrect(0x40FFFFFF), mobile.resurrect(-1)");

        Assert.All(result, value => Assert.False(value.Read<bool>()));
        Assert.Empty(_death.Raised);
    }

    [Fact]
    public void Kill_WhatTheDeathRefuses_SuchAsAPlayer_IsFalse()
    {
        _death.Kills = false;

        Assert.False(Run("return mobile.kill(2)")[0].Read<bool>());
    }

    [Fact]
    public void Criminal_SaysWhetherTheMobileIsOne_AndSetCriminalMakesOrPardonsIt()
    {
        var result = Run(
            "local before = mobile.criminal(2) " +
            "return before, mobile.set_criminal(2, true), mobile.criminal(2), mobile.set_criminal(2, false), " +
            "mobile.criminal(2), mobile.criminal(999), mobile.set_criminal(999, true)"
        );

        Assert.Equal([false, true, true, true, false], result.Take(5).Select(value => value.Read<bool>()));
        Assert.Equal((LuaValue.Nil, false), (result[5], result[6].Read<bool>()));
        Assert.Equal(["criminal 2", "pardon 2"], _crimes.Calls);
    }

    [Fact]
    public void BodyType_IsTheKindOfBodyOfTheMobile_AsTheBodiesFileSays()
    {
        _aria.Body = 400;
        _orc.Body = 17;

        var result = Run("return mobile.body_type(2), mobile.body_type(256), mobile.body_type(999)");

        Assert.Equal(((int)BodyType.Human, (int)BodyType.Monster, LuaValue.Nil), (result[0].Read<int>(), result[1].Read<int>(), result[2]));

        // A body the file does not list is empty.
        _orc.Body = 5000;
        Assert.Equal((int)BodyType.Empty, Run("return mobile.body_type(256)")[0].Read<int>());
    }

    [Fact]
    public void MessageCliloc_TellsThePlayerATextOfItsClient()
    {
        var result = Run("return mobile.message_cliloc(2, 500867), mobile.message_cliloc(2, 1042958, '3:05'), mobile.message_cliloc(2, 0), mobile.message_cliloc(999, 500867)");

        Assert.Equal([true, true, false, false], result.Select(value => value.Read<bool>()));
        Assert.Equal([(_aria, 500867, ""), (_aria, 1042958, "3:05")], _speech.ToldClilocs);
    }

    [Fact]
    public void Message_TellsThePlayer()
    {
        var result = Run("return mobile.message(2, 'That is too far away.')");

        Assert.True(result[0].Read<bool>());
        Assert.Equal((_aria, "That is too far away."), Assert.Single(_speech.Told));
    }

    [Fact]
    public void Message_ALongText_IsCut()
    {
        Run("return mobile.message(2, string.rep('a', 300))");

        Assert.Equal(128, Assert.Single(_speech.Told).Text.Length);
    }

    [Theory, InlineData("return mobile.message(999, 'hello')"), InlineData("return mobile.message(2, '  ')")]
    public void Message_AnUnknownMobileOrAnEmptyText_IsFalseAndTellsNobody(string chunk)
    {
        Assert.False(Run(chunk)[0].Read<bool>());

        Assert.Empty(_speech.Told);
    }

    [Fact]
    public void Template_IsTheIdOfTheMobilesTemplate_AndNilForAPlayerOrAnUnknownMobile()
    {
        var result = Run("return mobile.template(0x100), mobile.template(" + _aria.Id.Value + "), mobile.template(0x999)");

        Assert.Equal("orc", result[0].Read<string>());
        Assert.Equal([LuaValueType.Nil, LuaValueType.Nil], result[1..].Select(value => value.Type));
    }

    [Fact]
    public void SetStats_HandsOverTheNumbersGiven_AndLeavesTheOthersAlone()
    {
        var result = Run("return mobile.set_stats(0x100, { hits = 10, hits_max = 80, strength = 120, karma = -500 })");

        Assert.True(result[0].Read<bool>());
        var (mobile, change) = Assert.Single(_state.Stats);
        Assert.Same(_orc, mobile);
        Assert.Equal((10, 80, 120, -500), (change.Hits, change.HitsMax, change.Strength, change.Karma));
        Assert.Equal(
            (null, null, null, null, null, null, null),
            (change.Dexterity, change.Intelligence, change.Mana, change.ManaMax, change.Stamina, change.StaminaMax, change.Fame)
        );
    }

    [Fact]
    public void SetStats_EveryNumberOfStats_CanBeGiven()
    {
        Run(
            """
            return mobile.set_stats(0x100, { strength = 1, dexterity = 2, intelligence = 3, hits = 4, hits_max = 5,
                mana = 6, mana_max = 7, stamina = 8, stamina_max = 9, fame = 10, karma = 11 })
            """
        );

        var change = Assert.Single(_state.Stats).Change;
        Assert.Equal(
            (1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11),
            (change.Strength, change.Dexterity, change.Intelligence, change.Hits, change.HitsMax, change.Mana, change.ManaMax,
                change.Stamina, change.StaminaMax, change.Fame, change.Karma)
        );
    }

    [Theory]
    [InlineData("mobile.set_stats(0x100, { hit_points = 10 })")]
    [InlineData("mobile.set_stats(0x100, { body = 17 })")]
    [InlineData("mobile.set_stats(0x100, { hits = 'ten' })")]
    [InlineData("mobile.set_stats(0x100, { hits = 10.5 })")]
    [InlineData("mobile.set_stats(0x100, { hits = 10, mana = true })")]
    [InlineData("mobile.set_stats(0x100, { 10 })")]
    [InlineData("mobile.set_stats(0x100, {})")]
    [InlineData("mobile.set_stats(0x999, { hits = 10 })")]
    public void SetStats_AnUnknownNameABadValueOrAnUnknownMobile_ChangesNothing(string call)
    {
        Assert.False(Run("return " + call)[0].Read<bool>());

        Assert.Empty(_state.Stats);
    }

    [Fact]
    public void SetStats_WhatTheServiceRefuses_IsFalse()
    {
        _state.Result = false;

        Assert.False(Run("return mobile.set_stats(0x100, { strength = 70000 })")[0].Read<bool>());
    }

    [Fact]
    public void Skill_GivesTheValueAndTheCapInPoints_AndTheLockByName()
    {
        _state.Skills.Add(new() { Skill = SkillType.Magery, Base = 505, Cap = 1200, Lock = SkillLockType.Locked });

        var result = Run("local s = mobile.skill(0x100, 25) return s.value, s.cap, s.lock, mobile.skill(0x100, 0).value, mobile.skill(0x999, 25)");

        Assert.Equal([50.5, 120.0], result[..2].Select(value => value.Read<double>()));
        Assert.Equal("locked", result[2].Read<string>());
        Assert.Equal(0.0, result[3].Read<double>());
        Assert.Equal(LuaValueType.Nil, result[4].Type);
    }

    [Fact]
    public void Skills_GivesEverySkillAboveZeroByItsName()
    {
        _state.Skills.Add(new() { Skill = SkillType.Magery, Base = 505 });
        _state.Skills.Add(new() { Skill = SkillType.EvaluatingIntelligence, Base = 300 });

        var result = Run(
            """
            local all = mobile.skills(0x100)
            local count = 0
            for _ in pairs(all) do count = count + 1 end
            return all.magery, all.evaluating_intelligence, all.alchemy, count, mobile.skills(0x999)
            """
        );

        Assert.Equal([50.5, 30.0], result[..2].Select(value => value.Read<double>()));
        Assert.Equal(LuaValueType.Nil, result[2].Type);
        Assert.Equal(2, result[3].Read<int>());
        Assert.Equal(LuaValueType.Nil, result[4].Type);
    }

    [Fact]
    public void SetSkill_TakesPointsAndHandsOverTenths()
    {
        var result = Run("return mobile.set_skill(0x100, 25, 50.5), mobile.set_skill(0x100, 0, 100, 120)");

        Assert.Equal([true, true], result.Select(value => value.Read<bool>()));
        Assert.Equal(
            [(SkillType.Magery, 505, (int?)null), (SkillType.Alchemy, 1000, (int?)1200)],
            _state.SkillsSet.Select(set => (set.Skill, set.Value, set.Cap))
        );
    }

    [Fact]
    public void SetSkill_OfAnUnknownMobile_IsFalse()
    {
        Assert.False(Run("return mobile.set_skill(0x999, 25, 50)")[0].Read<bool>());

        Assert.Empty(_state.SkillsSet);
    }

    [Fact]
    public void SetNameBodyAndHue_HandOverWhatIsGiven()
    {
        var result = Run("return mobile.set_name(0x100, 'Grog'), mobile.set_body(0x100, 58), mobile.set_hue(0x100, 1153)");

        Assert.Equal([true, true, true], result.Select(value => value.Read<bool>()));
        Assert.Equal("Grog", Assert.Single(_state.Names).Name);
        Assert.Equal([((int?)58, (int?)null), (null, 1153)], _state.Looks.Select(look => (look.Body, look.Hue)));
    }

    [Fact]
    public void SetNameBodyAndHue_OfAnUnknownMobile_AreFalse()
    {
        var result = Run("return mobile.set_name(0x999, 'Grog'), mobile.set_body(0x999, 58), mobile.set_hue(0x999, 1153)");

        Assert.Equal([false, false, false], result.Select(value => value.Read<bool>()));
        Assert.Empty(_state.Names);
        Assert.Empty(_state.Looks);
    }

    [Fact]
    public void Flags_TellWhatTheMobileIs()
    {
        _orc.Hidden = true;
        _orc.WarMode = true;

        var result = Run("local f = mobile.flags(0x100) return f.hidden, f.frozen, f.war_mode, mobile.flags(0x999)");

        Assert.Equal([true, false, true], result[..3].Select(value => value.Read<bool>()));
        Assert.Equal(LuaValueType.Nil, result[3].Type);
    }

    [Fact]
    public void SetHiddenFrozenAndWarMode_HandOverWhatIsAsked()
    {
        var result = Run(
            "return mobile.set_hidden(0x100, true), mobile.set_frozen(0x100, true), mobile.set_war_mode(0x100, true), mobile.set_hidden(0x100, false)"
        );

        Assert.Equal([true, true, true, true], result.Select(value => value.Read<bool>()));
        Assert.Equal(["hidden 256 True", "frozen 256 True", "war 256 True", "hidden 256 False"], _state.Flags);
    }

    [Fact]
    public void SetHiddenFrozenAndWarMode_OfAnUnknownMobile_AreFalse()
    {
        var result = Run("return mobile.set_hidden(0x999, true), mobile.set_frozen(0x999, true), mobile.set_war_mode(0x999, true)");

        Assert.Equal([false, false, false], result.Select(value => value.Read<bool>()));
        Assert.Empty(_state.Flags);
    }

    private LuaValue[] Run(string chunk)
    {
        using var state = LuaState.Create();
        state.OpenBasicLibrary();
        state.OpenStringLibrary();
        new LuaModuleBinder(NoThreadGuard.Instance).Bind(state, new MobileModule(_mobiles, _teleports, _speech, _items, _music, _regions, _light, _state, _view, new StubDataLoaderService().With(new BodyContent { Body = new(400), Type = BodyType.Human }, new BodyContent { Body = new(17), Type = BodyType.Monster }), new StubWeightService { CarriedStones = 37, MaximumStones = 215 }, _crimes, _death));

        return SyncValueTask.Run(state.DoStringAsync(chunk, "t"));
    }
}
