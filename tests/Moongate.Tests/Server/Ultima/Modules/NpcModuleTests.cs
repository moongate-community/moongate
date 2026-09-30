using Lua;
using Lua.Standard;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Core.Types.Geometry;
using Moongate.Scripting.Binding;
using Moongate.Scripting.Internal;
using Moongate.Scripting.Utils;
using Moongate.Server.Ultima.Data.Templates.Mobiles;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Modules;
using Moongate.Server.Ultima.Services;
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
            new MobileTemplate { Id = "quiet" }
        )
    );
    private readonly MobileEntity _player = new()
    {
        Id = new Serial(2), Name = "Aria", AccountId = new Serial(0x42), Map = MapType.Trammel,
        Location = new Point3D(1601, 1600, 0)
    };

    public NpcModuleTests()
    {
        _mobiles = new(_movement, TestSectors.Create());
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

    private LuaValue[] Run(string chunk)
    {
        using var state = LuaState.Create();
        state.OpenBasicLibrary();
        state.OpenStringLibrary();
        new LuaModuleBinder(NoThreadGuard.Instance).Bind(state, new NpcModule(_mobiles, _speech, _view, _templates));

        return SyncValueTask.Run(state.DoStringAsync(chunk, "t"));
    }
}
