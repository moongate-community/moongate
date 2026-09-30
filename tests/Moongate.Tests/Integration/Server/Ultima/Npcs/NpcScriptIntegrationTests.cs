using DryIoc;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Core.Types.Geometry;
using Moongate.Scripting.Data.Config;
using Moongate.Scripting.Data.Events;
using Moongate.Scripting.Extensions.Scripts;
using Moongate.Scripting.Interfaces;
using Moongate.Scripting.Services;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Events;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Templates.Mobiles;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Modules;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Scripting;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Tests.TestSupport.Ultima.Sectors;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Tests.TestSupport.Ultima.World;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Integration.Server.Ultima.Npcs;

public sealed class NpcScriptIntegrationTests : IDisposable
{
    private readonly TemporaryScriptsDirectory _scripts = new();
    private readonly Container _container = new();
    private readonly StubGameLoop _loop = new();
    private readonly RecordingTimerService _timers = new();
    private readonly RecordingSpeechService _speech = new();
    private readonly RecordingWorldViewService _view = new();
    private readonly List<ScriptErrorEvent> _errors = [];
    private readonly SectorService _sectors = TestSectors.Create();
    private readonly MobileService _mobiles;
    private readonly MobileTemplateService _templates = new(
        new StubDataLoaderService().With(new MobileTemplate { Id = "cat", ScriptId = "greeter" })
    );
    private readonly MobileEntity _cat = new()
    {
        Id = new Serial(0x100), Name = "a cat", TemplateId = "cat", Map = MapType.Trammel,
        Location = new Point3D(1600, 1600, 0), Direction = DirectionType.North
    };
    private readonly MobileEntity _aria = new()
    {
        Id = new Serial(2), Name = "Aria", AccountId = new Serial(0x42), Map = MapType.Trammel,
        Location = new Point3D(1605, 1600, 0)
    };

    public NpcScriptIntegrationTests()
    {
        _mobiles = new(new StubMovementService(), _sectors);
        _mobiles.EnterWorld(_cat);
        _mobiles.EnterWorld(_aria);
        _container.RegisterMoongateEventBus();
        _container.RegisterInstance<IGameLoopService>(_loop);
        _container.RegisterInstance<ITimerService>(_timers);
        _container.RegisterInstance<IMobileService>(_mobiles);
        _container.RegisterInstance<ISpeechService>(_speech);
        _container.RegisterInstance<IWorldViewService>(_view);
        _container.AddScriptModule<NpcModule>();
        _container.AddScriptModule<DiceModule>();
        _container.Resolve<IMoongateEventBus>()
            .Subscribe<ScriptErrorEvent>((evt, _) =>
                {
                    _errors.Add(evt);

                    return Task.CompletedTask;
                }
            );
    }

    [Fact]
    public async Task AThink_RunsTheTemplateScriptWhichActsThroughTheNpcModule()
    {
        _scripts.Write(
            "mobiles/greeter.lua",
            """
            greeter = {}

            function greeter.on_think(serial)
                npc.say(serial, "Meow, I am " .. npc.name(serial))
                npc.step(serial, DirectionType.North)
            end
            """
        );
        using var engine = NewEngine();
        await engine.StartAsync();
        var scripts = NewScripts(engine);
        await scripts.StartAsync();

        scripts.Think(_cat);

        Assert.Empty(_errors);
        Assert.Equal((_cat, "Meow, I am a cat"), Assert.Single(_speech.Said));
        Assert.Equal(new Point3D(1600, 1599, 0), _cat.Location);
    }

    [Fact]
    public async Task APlayerSpeaking_RunsOnSpeechWhichMayAnswerLater()
    {
        _scripts.Write(
            "mobiles/greeter.lua",
            """
            greeter = {}

            function greeter.on_speech(serial, speaker, text)
                if text:lower():find("hello", 1, true) then
                    wait(1)
                    npc.say(serial, "Hello to you, " .. speaker)
                end
            end
            """
        );
        using var engine = NewEngine();
        await engine.StartAsync();
        var scripts = NewScripts(engine);
        await scripts.StartAsync();

        new NpcHearingService(scripts, _sectors).Heard(_aria, "HELLO cat");
        Assert.Empty(_speech.Said);
        _timers.Fire(_timers.Timers.Single().Id);

        Assert.Empty(_errors);
        Assert.Equal("Hello to you, 2", Assert.Single(_speech.Said).Text);
    }

    [Fact]
    public async Task TheShippedWanderScript_StepsEveryFourthThinkAndAnswersAGreeting()
    {
        _scripts.Write("mobiles/wander.lua", File.ReadAllText(ShippedScript("mobiles/wander.lua")));
        var templates = new MobileTemplateService(
            new StubDataLoaderService().With(new MobileTemplate { Id = "cat", ScriptId = "wander" })
        );
        using var engine = NewEngine();
        await engine.StartAsync();
        var scripts = new NpcScriptService(
            engine,
            templates,
            _loop,
            new ScriptEngineOptions { ScriptsDirectory = _scripts.Path }
        );
        await scripts.StartAsync();

        for (var think = 0; think < 3; think++)
        {
            scripts.Think(_cat);
        }

        Assert.Empty(_view.Calls);
        scripts.Think(_cat);
        Assert.Single(_view.Calls);

        new NpcHearingService(scripts, _sectors).Heard(_aria, "Hello!");
        _timers.Fire(_timers.Timers.Single().Id);

        Assert.Empty(_errors);
        Assert.Equal("Well met, traveller.", Assert.Single(_speech.Said).Text);
    }

    [Fact]
    public async Task AMobileScriptWithASyntaxError_IsReportedAndTheServerStartsWithTheOthers()
    {
        _scripts.Write("mobiles/broken.lua", "broken = {} function broken.on_think(serial) npc.say(serial, end");
        _scripts.Write("mobiles/greeter.lua", "greeter = {} function greeter.on_think(serial) npc.say(serial, 'ok') end");
        using var engine = NewEngine();
        await engine.StartAsync();
        var scripts = NewScripts(engine);

        await scripts.StartAsync();
        scripts.Think(_cat);

        Assert.Single(_errors);
        Assert.Equal("ok", Assert.Single(_speech.Said).Text);
    }

    [Theory, InlineData("orione"), InlineData("vega")]
    public async Task TheShippedCatScripts_TalkAndMeowWithoutErrors(string cat)
    {
        _scripts.Write($"mobiles/{cat}.lua", File.ReadAllText(ShippedScript($"mobiles/{cat}.lua")));
        var templates = new MobileTemplateService(
            new StubDataLoaderService().With(new MobileTemplate { Id = "cat", ScriptId = cat })
        );
        using var engine = NewEngine();
        await engine.StartAsync();
        var scripts = new NpcScriptService(
            engine,
            templates,
            _loop,
            new ScriptEngineOptions { ScriptsDirectory = _scripts.Path }
        );
        await scripts.StartAsync();

        for (var think = 0; think < 12; think++)
        {
            scripts.Think(_cat);
        }

        Assert.Empty(_errors);
        Assert.Equal(3, _speech.Said.Count);
        Assert.All(_speech.Said, said => Assert.StartsWith("M", said.Text));
        Assert.Equal(2, _speech.Sounds.Count);
        Assert.All(_speech.Sounds, sound => Assert.Contains(sound.Sound, new[] { 0x69, 0x6A }));
    }

    public void Dispose()
    {
        _container.Dispose();
        _scripts.Dispose();
    }

    private static string ShippedScript(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Moongate.slnx")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(directory!.FullName, "moongate_root", "scripts", relativePath);
    }

    private NpcScriptService NewScripts(IScriptEngine engine)
    {
        return new(engine, _templates, _loop, new ScriptEngineOptions { ScriptsDirectory = _scripts.Path });
    }

    private LuaScriptEngineService NewEngine()
    {
        var options = new ScriptEngineOptions
        {
            ScriptsDirectory = _scripts.Path,
            MaxInstructionsPerResume = 20_000,
            MaxInstructionsPerChunk = 100_000,
            HookInterval = 100,
            WriteDefinitions = false
        };

        return new(
            options,
            _container.Resolve<IScriptModuleRegistry>(),
            _container,
            _loop,
            _timers,
            new EventBusAdapter(_container)
        );
    }
}
