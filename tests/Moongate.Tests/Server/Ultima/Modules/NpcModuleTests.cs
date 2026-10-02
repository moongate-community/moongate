using Lua;
using Lua.Standard;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Core.Types.Geometry;
using Moongate.Scripting.Binding;
using Moongate.Scripting.Internal;
using Moongate.Scripting.Utils;
using Moongate.Server.Ultima.Data.Templates.Mobiles;
using Moongate.Scripting.Interfaces;
using Moongate.Tests.Support.Timing;
using Moongate.Tests.TestSupport.Scripting;
using Moongate.Tests.TestSupport.Ultima.Mobiles;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Modules;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Types.Mobiles;
using Moongate.Server.Ultima.Types.Movement;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Tests.TestSupport.Ultima.Sectors;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Tests.TestSupport.Ultima.World;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Modules;

public sealed class NpcModuleTests
{
    private readonly StubMovementService _movement = new();
    private readonly RecordingSpeechService _speech = new();
    private readonly RecordingWorldViewService _view = new();
    private readonly MobileService _mobiles;
    private readonly StubNpcService _npcs = new();
    private readonly RecordingMoveOverService _moveOver = new();
    private readonly StubPathfindingService _finder = new();
    private readonly ManualTimeProvider _time = new();
    private readonly NpcPathService _paths;
    private readonly FakeScriptEngine _engine = new() { CurrentScript = "mobiles/summoner.lua" };
    private readonly StubGameLoop _loop = new();
    private readonly SectorService _sectors = TestSectors.Create();
    private readonly MobileEntity _orc = new()
    {
        Id = new Serial(0x100), Name = "an orc", TemplateId = "orc", Map = MapType.Trammel,
        Location = new Point3D(1600, 1600, 0), Direction = DirectionType.North
    };
    private readonly MobileEntity _silentOrc = new()
    {
        Id = new Serial(0x101), Name = "a quiet orc", TemplateId = "quiet", Map = MapType.Trammel,
        Location = new Point3D(1602, 1600, 0)
    };
    private readonly MobileTemplateService _templates = new(
        new StubDataLoaderService().With(
            new MobileTemplate
            {
                Id = "orc", Sounds = new MobileSounds { StartAttack = 0x69, Idle = 0x2A3, Attack = 0x6B, Hurt = 0x6C, Death = 0x6D }
            },
            new MobileTemplate { Id = "quiet" },
            new MobileTemplate { Id = "dolphin", Movement = MobileMovementType.Water },
            new MobileTemplate { Id = "walrus", Movement = MobileMovementType.Both }
        )
    );
    private readonly MobileEntity _player = new()
    {
        Id = new Serial(2), Name = "Aria", AccountId = new Serial(0x42), Map = MapType.Trammel,
        Location = new Point3D(1601, 1600, 0)
    };

    public NpcModuleTests()
    {
        _mobiles = new(_movement, _sectors);
        _paths = new(_finder, _time);
        _mobiles.EnterWorld(_orc);
        _mobiles.EnterWorld(_player);
        _mobiles.EnterWorld(_silentOrc);
    }

    [Fact]
    public void Say_MakesTheNpcSpeak()
    {
        var result = Run("return npc.say(256, 'Grr')");

        Assert.True(result[0].Read<bool>());
        Assert.Equal((_orc, "Grr"), Assert.Single(_speech.Said));
    }

    [Fact]
    public void Say_ALongText_IsCutTo128Characters()
    {
        Run("return npc.say(256, string.rep('a', 200))");

        Assert.Equal(128, Assert.Single(_speech.Said).Text.Length);
    }

    [Theory,
     InlineData("return npc.say(2, 'I am a puppet')"),
     InlineData("return npc.say(999, 'nobody')"),
     InlineData("return npc.say(-1, 'nobody')"),
     InlineData("return npc.say(256, '   ')")]
    public void Say_NotAnNpcInTheWorldOrBlank_IsFalseAndSilent(string chunk)
    {
        var result = Run(chunk);

        Assert.False(result[0].Read<bool>());
        Assert.Empty(_speech.Said);
    }

    [Fact]
    public void Step_InTheDirectionItFaces_MovesAndShowsIt()
    {
        var result = Run("return npc.step(256, 'North')");

        Assert.True(result[0].Read<bool>());
        Assert.Equal(new Point3D(1600, 1599, 0), _orc.Location);
        Assert.Equal(["Moved 256 1600,1600,0"], _view.Calls);
    }

    [Theory,
     InlineData("orc", MovementAbilityType.Walk),
     InlineData("dolphin", MovementAbilityType.Swim),
     InlineData("walrus", MovementAbilityType.Walk | MovementAbilityType.Swim)]
    public void Step_MovesAsTheTemplateSays(string templateId, MovementAbilityType expected)
    {
        _orc.TemplateId = templateId;

        Run("return npc.step(256, 'North')");

        Assert.Equal([expected], _movement.Abilities);
    }

    [Fact]
    public void Step_InANewDirection_TurnsAndMovesInOneCall()
    {
        var result = Run("return npc.step(256, 'East')");

        Assert.True(result[0].Read<bool>());
        Assert.Equal((DirectionType.East, new Point3D(1601, 1600, 0)), (_orc.Direction, _orc.Location));
        Assert.Equal(["Moved 256 1600,1600,0"], _view.Calls);
    }

    [Fact]
    public void Step_Blocked_TurnsButStaysAndIsFalse()
    {
        _movement.Allow = false;

        var result = Run("return npc.step(256, 'East')");

        Assert.False(result[0].Read<bool>());
        Assert.Equal((DirectionType.East, new Point3D(1600, 1600, 0)), (_orc.Direction, _orc.Location));
        Assert.Equal(["Moved 256 1600,1600,0"], _view.Calls);
    }

    [Fact]
    public void Step_Running_RunsAndShowsItAsARun()
    {
        var result = Run("return npc.step(256, 'North', true)");

        Assert.True(result[0].Read<bool>());
        Assert.Equal(new Point3D(1600, 1599, 0), _orc.Location);
        Assert.Equal(["Moved 256 1600,1600,0 run"], _view.Calls);
    }

    [Fact]
    public void Step_TheRunningFlagAsADirection_IsFalseAndDoesNothing()
    {
        var result = Run("return npc.step(256, 'Running')");

        Assert.False(result[0].Read<bool>());
        Assert.Equal((DirectionType.North, new Point3D(1600, 1600, 0)), (_orc.Direction, _orc.Location));
        Assert.Empty(_view.Calls);
    }

    [Fact]
    public void PlaySound_PlaysItWhereTheNpcStands()
    {
        var result = Run("return npc.play_sound(256, 0x69)");

        Assert.True(result[0].Read<bool>());
        Assert.Equal((_orc, 0x69), Assert.Single(_speech.Sounds));
    }

    [Theory,
     InlineData("idle", 0x2A3),
     InlineData("start_attack", 0x69),
     InlineData("attack", 0x6B),
     InlineData("hurt", 0x6C),
     InlineData("death", 0x6D)]
    public void PlaySound_AKindOfItsTemplate_PlaysThatSound(string kind, int expected)
    {
        var result = Run($"return npc.play_sound(256, '{kind}')");

        Assert.True(result[0].Read<bool>());
        Assert.Equal((_orc, expected), Assert.Single(_speech.Sounds));
    }

    [Theory,
     InlineData("return npc.play_sound(2, 0x69)"),
     InlineData("return npc.play_sound(256, -1)"),
     InlineData("return npc.play_sound(256, 0x10000)"),
     InlineData("return npc.play_sound(256, 1.5)"),
     InlineData("return npc.play_sound(256, 'purr')"),
     InlineData("return npc.play_sound(256, 'Idle')"),
     InlineData("return npc.play_sound(256, true)"),
     InlineData("return npc.play_sound(257, 'idle')")]
    public void PlaySound_APlayerASoundOutOfRangeOrAKindTheTemplateLacks_IsFalse(string chunk)
    {
        Assert.False(Run(chunk)[0].Read<bool>());
        Assert.Empty(_speech.Sounds);
    }

    [Fact]
    public void SetProp_StoresStringsNumbersAndBools_GetPropReadsThemBack()
    {
        var result = Run(
            "npc.set_prop(256, 'vega.greeted', 3) npc.set_prop(256, 'vega.mood', 'sleepy') npc.set_prop(256, 'vega.fed', true) " +
            "npc.set_prop(256, 'vega.weight', 4.5) " +
            "return npc.get_prop(256, 'vega.greeted'), npc.get_prop(256, 'vega.mood'), npc.get_prop(256, 'vega.fed'), " +
            "npc.get_prop(256, 'vega.weight'), npc.get_prop(256, 'missing')"
        );

        Assert.Equal(3d, result[0].Read<double>());
        Assert.Equal("sleepy", result[1].Read<string>());
        Assert.True(result[2].Read<bool>());
        Assert.Equal(4.5, result[3].Read<double>());
        Assert.Equal(LuaValue.Nil, result[4]);
        Assert.Equal(3L, _orc.GetProp<long>("vega.greeted"));
    }

    [Fact]
    public void SetProp_Nil_RemovesIt()
    {
        _orc.SetProp("vega.greeted", 3);

        var result = Run("return npc.set_prop(256, 'vega.greeted', nil), npc.get_prop(256, 'vega.greeted')");

        Assert.True(result[0].Read<bool>());
        Assert.Equal(LuaValue.Nil, result[1]);
        Assert.Null(_orc.Props);
    }

    [Theory,
     InlineData("return npc.set_prop(256, 'bag', {})"),
     InlineData("return npc.set_prop(256, 'fn', print)"),
     InlineData("return npc.set_prop(256, '', 1)"),
     InlineData("return npc.set_prop(256, '  ', 1)"),
     InlineData("return npc.set_prop(2, 'x', 1)"),
     InlineData("return npc.set_prop(999, 'x', 1)")]
    public void SetProp_ATableAFunctionABlankKeyOrNotAnNpc_IsFalseAndStoresNothing(string chunk)
    {
        Assert.False(Run(chunk)[0].Read<bool>());
        Assert.Null(_orc.Props);
        Assert.Null(_player.Props);
    }

    [Fact]
    public void GetProp_OfAPlayerOrUnknown_IsNil()
    {
        _player.SetProp("secret", 1);

        var result = Run("return npc.get_prop(2, 'secret'), npc.get_prop(999, 'secret')");

        Assert.All(result, value => Assert.Equal(LuaValue.Nil, value));
    }

    [Fact]
    public void Step_APlayer_DoesNothing()
    {
        var result = Run("return npc.step(2, 'North')");

        Assert.False(result[0].Read<bool>());
        Assert.Equal(new Point3D(1601, 1600, 0), _player.Location);
        Assert.Empty(_view.Calls);
    }

    [Fact]
    public void Location_GivesTheCoordinatesAndMap()
    {
        var result = Run("local l = npc.location(256) return l.x, l.y, l.z, l.map");

        Assert.Equal([1600d, 1600d, 0d, (double)(int)MapType.Trammel], result.Select(value => value.Read<double>()));
    }

    [Fact]
    public void NameAndLocation_OfAnUnknownSerial_AreNil()
    {
        var result = Run("return npc.name(999), npc.location(999), npc.name(2)");

        Assert.All(result, value => Assert.Equal(LuaValue.Nil, value));
    }

    [Fact]
    public void Name_IsTheNpcName()
    {
        Assert.Equal("an orc", Run("return npc.name(256)")[0].Read<string>());
    }

    [Fact]
    public void Step_OntoACellWithNoItem_TellsNobody()
    {
        Run("npc.step(256, 'North')");

        Assert.Empty(_moveOver.Steps);
        Assert.Equal(0, _loop.PostedWorkItems);
    }

    [Fact]
    public void Step_ThatMoves_TellsTheItemsOfTheNewCell_ABlockedOneDoesNot()
    {
        var items = TestItems.Create(_sectors);
        var pad = new ItemEntity { Id = new Serial(0x40000010), TemplateId = "decoration_teleporter", ItemId = 0x1BC3, Amount = 1 };
        items.Add([pad]);
        items.PlaceOnGround(pad, MapType.Trammel, new Point3D(1600, 1599, 0));

        Run("npc.step(256, 'North')");
        _movement.Allow = false;
        Run("npc.step(256, 'North')");

        Assert.Equal([new Point3D(1600, 1599, 0)], _moveOver.Steps);
    }

    [Fact]
    public void WalkTo_TakesOneStepOfThePathPerCall_AndSaysWhenItArrived()
    {
        _finder.Finds(DirectionType.North, DirectionType.North);

        var result = Run(
            "return npc.walk_to(256, 1600, 1598, 0), npc.walk_to(256, 1600, 1598, 0), npc.walk_to(256, 1600, 1598, 0)"
        );

        Assert.Equal(["moving", "moving", "arrived"], result.Select(value => value.Read<string>()));
        Assert.Equal(new Point3D(1600, 1598, 0), _orc.Location);
        // One search for the whole way, and the players around saw both steps.
        Assert.Single(_finder.Searches);
        Assert.Equal(["Moved 256 1600,1600,0", "Moved 256 1600,1599,0"], _view.Calls);
    }

    [Fact]
    public void WalkTo_WithoutAHeight_AimsAtTheGroundOfThePlace_OnTheNpcsStoreyFirst()
    {
        _movement.SpawnZ = (_, _) => 7;
        _finder.Finds(DirectionType.North);

        Run("npc.walk_to(256, 1600, 1598)");

        Assert.Equal(new Point3D(1600, 1598, 7), Assert.Single(_finder.Searches).To);
        // Asked no higher than the NPC's head: a floor above it is not the place.
        Assert.Equal(16, _movement.SpawnCeilings[0]);
    }

    [Fact]
    public void WalkTo_WithANilHeightAndARange_Works()
    {
        _finder.Finds(DirectionType.North);

        Assert.Equal("moving", Run("return npc.walk_to(256, 1600, 1590, nil, 1, true)")[0].Read<string>());
        Assert.Equal("moving", Run("return npc.walk_to(256, 1600, 1590, nil, nil, true)")[0].Read<string>());
    }

    [Fact]
    public void WalkTo_WithARange_ArrivesNextToThePlace()
    {
        Assert.Equal("arrived", Run("return npc.walk_to(256, 1601, 1601, 0, 1)")[0].Read<string>());

        Assert.Empty(_finder.Searches);
    }

    [Fact]
    public void WalkTo_WithNoPath_SaysSo_AndABlockedStepIsBlocked()
    {
        Assert.Equal("no_path", Run("return npc.walk_to(256, 1600, 1598, 0)")[0].Read<string>());

        _time.Advance(TimeSpan.FromSeconds(10));
        _finder.Finds(DirectionType.North);
        _movement.Allow = false;

        Assert.Equal("blocked", Run("return npc.walk_to(256, 1600, 1598, 0)")[0].Read<string>());
        // Still blocked while it waits to search again.
        Assert.Equal("blocked", Run("return npc.walk_to(256, 1600, 1598, 0)")[0].Read<string>());
        Assert.Equal(2, _finder.Searches.Count);
        Assert.Equal(new Point3D(1600, 1600, 0), _orc.Location);
    }

    [Theory,
     InlineData("return npc.walk_to(2, 1600, 1598)"),
     InlineData("return npc.walk_to(999, 1600, 1598)"),
     InlineData("return npc.walk_to(256, 1600, 1598, 300)"),
     InlineData("return npc.walk_to(256, 1600, 1598, 0, -1)"),
     InlineData("return npc.find_path(2, 1600, 1598)"),
     InlineData("return npc.find_path(256, 1600, 1598)")]
    public void WalkToAndFindPath_ForAPlayerABadPlaceOrNoPath_AreNil(string chunk)
    {
        Assert.Equal(LuaValue.Nil, Run(chunk)[0]);
    }

    [Fact]
    public void FindPath_GivesTheStepsAsDirections()
    {
        _finder.Finds(DirectionType.North, DirectionType.NorthEast);

        var result = Run("local steps = npc.find_path(256, 1601, 1598, 0, true) return #steps, steps[1], steps[2]");

        Assert.Equal([2, (int)DirectionType.North, (int)DirectionType.NorthEast], result.Select(value => value.Read<int>()));
        Assert.True(Assert.Single(_finder.Searches).AllowPartial);
    }

    [Fact]
    public void Spawn_AsksForTheNpc_AndGivesItsSerialToTheCallback()
    {
        var result = Run("return npc.spawn('orc', 'Trammel', 1500, 1600, 10, function(serial) end)");

        Assert.True(result[0].Read<bool>());
        Assert.Equal(("orc", MapType.Trammel, new Point3D(1500, 1600, 10)), Assert.Single(_npcs.Spawns));
        var call = Assert.Single(_engine.FunctionCalls);
        Assert.Equal(("mobiles/summoner.lua", (object)0x100L), (call.Owner, Assert.Single(call.Args)));
    }

    [Fact]
    public void Spawn_WithoutACallback_OnlySpawns()
    {
        Assert.True(Run("return npc.spawn('orc', 'Trammel', 1500, 1600, 10)")[0].Read<bool>());

        Assert.Single(_npcs.Spawns);
        Assert.Empty(_engine.FunctionCalls);
    }

    [Fact]
    public void Spawn_ThatFails_CallsNothingAndRaisesNothing()
    {
        _npcs.SpawnFailure = new InvalidOperationException("no database");

        Assert.True(Run("return npc.spawn('orc', 'Trammel', 1500, 1600, 10, function(serial) end)")[0].Read<bool>());

        Assert.Empty(_engine.FunctionCalls);
    }

    [Theory,
     InlineData("return npc.spawn('dragon', 'Trammel', 1500, 1600, 10)"),
     InlineData("return npc.spawn('orc', 'Trammel', -1, 1600, 10)"),
     InlineData("return npc.spawn('orc', 'Tokuno', 100, 100, 0)"),
     InlineData("return npc.spawn('orc', 'Trammel', 1500, 1600, 200)")]
    public void Spawn_WhatCannotBeSpawned_IsFalseAndAsksNothing(string chunk)
    {
        Assert.False(Run(chunk)[0].Read<bool>());

        Assert.Empty(_npcs.Spawns);
    }

    [Fact]
    public void Spawn_WithACallbackThatIsNotAFunction_IsAnArgumentError()
    {
        Assert.ThrowsAny<Exception>(() => Run("return npc.spawn('orc', 'Trammel', 1500, 1600, 10, 5)"));
    }

    [Fact]
    public void Delete_RemovesAnNpc_NeverAPlayer()
    {
        var result = Run("return npc.delete(256), npc.delete(2), npc.delete(999)");

        Assert.True(result[0].Read<bool>());
        Assert.False(result[1].Read<bool>());
        Assert.False(result[2].Read<bool>());
        Assert.Equal([_orc.Id], _npcs.Removals);
    }

    [Fact]
    public void Face_TurnsTheNpcTowardsThePlace_AndShowsTheTurn()
    {
        var result = Run("return npc.face(256, 1605, 1600), npc.face(256, 1605, 1600), npc.face(256, 1600, 1600), npc.face(2, 1605, 1600)");

        Assert.True(result[0].Read<bool>());
        Assert.True(result[1].Read<bool>());
        Assert.False(result[2].Read<bool>());
        Assert.False(result[3].Read<bool>());
        Assert.Equal(DirectionType.East, _orc.Direction);
        // Once: already facing east the second time.
        Assert.Equal(["Moved 256 1600,1600,0"], _view.Calls);
    }

    [Fact]
    public void DistanceTo_CountsTilesAsTheViewRangeDoes()
    {
        var result = Run("return npc.distance_to(256, 1603, 1595), npc.distance_to(256, 1600, 1600), npc.distance_to(999, 1, 1)");

        Assert.Equal((5, 0), (result[0].Read<int>(), result[1].Read<int>()));
        Assert.Equal(LuaValue.Nil, result[2]);
    }

    private LuaValue[] Run(string chunk)
    {
        using var state = LuaState.Create();
        state.OpenBasicLibrary();
        state.OpenStringLibrary();
        new LuaModuleBinder(NoThreadGuard.Instance).Bind(state, new NpcModule(_mobiles, _speech, _view, _templates, _npcs, new Lazy<IScriptEngine>(() => _engine), _loop, _sectors, _moveOver, _paths, _finder, _movement));

        return SyncValueTask.Run(state.DoStringAsync(chunk, "t"));
    }
}
