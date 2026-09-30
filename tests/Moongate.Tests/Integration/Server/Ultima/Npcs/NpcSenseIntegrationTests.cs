using DryIoc;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Core.Types.Geometry;
using Moongate.Scripting.Data.Config;
using Moongate.Scripting.Data.Events;
using Moongate.Scripting.Extensions.Scripts;
using Moongate.Scripting.Services;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Events;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Config;
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

public sealed class NpcSenseIntegrationTests : IDisposable
{
    private readonly TemporaryScriptsDirectory _scripts = new();
    private readonly Container _container = new();
    private readonly StubGameLoop _loop = new();
    private readonly RecordingTimerService _timers = new();
    private readonly RecordingSpeechService _speech = new();
    private readonly List<ScriptErrorEvent> _errors = [];
    private readonly LuaScriptEngineService _engine;
    private readonly NpcScriptService _npcScripts;
    private readonly MobileService _mobiles;

    public NpcSenseIntegrationTests()
    {
        var sectors = TestSectors.Create();
        _engine = new(
            new ScriptEngineOptions
            {
                ScriptsDirectory = _scripts.Path,
                MaxInstructionsPerResume = 20_000,
                MaxInstructionsPerChunk = 100_000,
                HookInterval = 100
            },
            RegisterContainer(),
            _container,
            _loop,
            _timers,
            new EventBusAdapter(_container)
        );
        _npcScripts = new(
            _engine,
            new MobileTemplateService(new StubDataLoaderService().With(new MobileTemplate { Id = "orc", ScriptId = "watcher" })),
            _loop,
            new ScriptEngineOptions { ScriptsDirectory = _scripts.Path }
        );
        _mobiles = new(new StubMovementService(), sectors, new NpcSenseService(_npcScripts, sectors, new NpcsConfig()));
        _container.RegisterInstance<IMobileService>(_mobiles);
    }

    [Fact]
    public async Task AStepInsideAThink_LetsBothNpcsSenseEachOtherRightAfter()
    {
        _scripts.Write(
            "mobiles/watcher.lua",
            """
            watcher = {}

            local stepped = {}

            function watcher.on_think(serial)
                if not stepped[serial] then
                    stepped[serial] = true
                    npc.step(serial, DirectionType.North)
                end
            end

            function watcher.on_mobile_in_range(serial, other)
                npc.say(serial, "I sense " .. other)
            end
            """
        );
        var orc = Npc(0x100, 1600, 1600);
        var other = Npc(0x101, 1600, 1591);
        _mobiles.EnterWorld(orc);
        _mobiles.EnterWorld(other);
        await _engine.StartAsync();
        await _npcScripts.StartAsync();
        _loop.DeferTryPost = true;

        _npcScripts.Think(orc);

        Assert.Equal(new Point3D(1600, 1599, 0), orc.Location);
        Assert.Empty(_speech.Said);
        _loop.RunDeferred();
        Assert.Empty(_errors);
        Assert.Equal(
            ["an orc: I sense 256", "an orc: I sense 257"],
            _speech.Said.Select(said => $"{said.Speaker.Name}: {said.Text}").Order()
        );
    }

    public void Dispose()
    {
        _engine.Dispose();
        _container.Dispose();
        _scripts.Dispose();
    }

    private Moongate.Scripting.Interfaces.IScriptModuleRegistry RegisterContainer()
    {
        _container.RegisterMoongateEventBus();
        _container.RegisterInstance<IGameLoopService>(_loop);
        _container.RegisterInstance<ITimerService>(_timers);
        _container.RegisterInstance<ISpeechService>(_speech);
        _container.RegisterInstance<IWorldViewService>(new RecordingWorldViewService());
        _container.RegisterInstance<IMobileTemplateService>(new MobileTemplateService(new StubDataLoaderService()));
        _container.AddScriptModule<NpcModule>();
        _container.Resolve<IMoongateEventBus>()
            .Subscribe<ScriptErrorEvent>((evt, _) =>
                {
                    _errors.Add(evt);

                    return Task.CompletedTask;
                }
            );

        return _container.Resolve<Moongate.Scripting.Interfaces.IScriptModuleRegistry>();
    }

    private static MobileEntity Npc(uint serial, int x, int y)
    {
        return new()
        {
            Id = new Serial(serial), Name = "an orc", TemplateId = "orc", Map = MapType.Trammel,
            Location = new Point3D(x, y, 0), Direction = DirectionType.North
        };
    }
}
