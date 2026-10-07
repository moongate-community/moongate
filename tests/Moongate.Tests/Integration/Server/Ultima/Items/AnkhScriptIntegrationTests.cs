using DryIoc;
using Moongate.Core.Directories;
using Moongate.Tests.TestSupport.Ultima.Effects;
using Moongate.Tests.TestSupport.Ultima.Death;
using Moongate.Server.Ultima.Loaders;
using Moongate.Server.Ultima.Types.Effects;
using Moongate.Server.Ultima.Data.Effects;
using Moongate.Scripting.Types.Scripts;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Scripting.Data.Config;
using Moongate.Scripting.Data.Events;
using Moongate.Scripting.Extensions.Scripts;
using Moongate.Scripting.Interfaces;
using Moongate.Scripting.Services;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Events;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Gumps;
using Moongate.Server.Ultima.Data.Templates.Gumps;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Modules;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Localization;
using Moongate.Tests.TestSupport.Scripting;
using Moongate.Tests.TestSupport.Ultima.Bank;
using Moongate.Tests.TestSupport.Ultima.Gumps;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Moongates;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Tests.TestSupport.Ultima.Tooltips;
using Moongate.Tests.TestSupport.Ultima.World;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Integration.Server.Ultima.Items;

/// <summary>
///     Runs the ankh script and the gump it opens, shipped in <c>moongate_root</c>, with the real Lua engine and the
///     real gump module: who may use an ankh and what its Continue button does.
/// </summary>
public sealed class AnkhScriptIntegrationTests : IAsyncLifetime
{
    private const int ContinueButton = 1;
    private const int TooFarCliloc = 500446;

    private static readonly Point3D Spot = new(1336, 1997, 5);

    private readonly TemporaryScriptsDirectory _scripts = new();
    private readonly Container _container = new();
    private readonly StubGameLoop _loop = new();
    private readonly RecordingTimerService _timers = new();
    private readonly RecordingGumpService _gumps = new();
    private readonly RecordingSpeechService _speech = new();
    private readonly RecordingWorldViewService _view = new();
    private readonly StubDeathService _death = new();
    private readonly RecordingEffectService _effects = new();
    private readonly List<ScriptErrorEvent> _errors = [];

    private readonly ItemEntity _ankh = new()
    {
        Id = new Serial(0x40000040), TemplateId = "decoration_ankh", ItemId = 0x0003, Amount = 1
    };

    private BroadcastFixture _fixture = null!;
    private GameSession _session = null!;
    private LuaScriptEngineService _engine = null!;
    private ItemScriptService _itemScripts = null!;
    private MobileEntity _aria = null!;
    private ItemService _items = null!;

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        _session = await _fixture.AddAsync(2);
        Assert.True(_fixture.Mobiles.TryGet(new Serial(2), out _aria!));
        _aria.Map = MapType.Trammel;
        _aria.Location = Spot;
        _items = TestItems.Create(_fixture.Sectors);
        var items = _items;
        _ankh.PlaceOnGround(MapType.Trammel, Spot);
        items.Add([_ankh]);
        var root = Path.Combine(RepositoryRoot(), "moongate_root");
        _scripts.Write("items/ankh.lua", await File.ReadAllTextAsync(Path.Combine(root, "scripts", "items", "ankh.lua")));
        _scripts.Write(
            "gumps/resurrect.lua",
            await File.ReadAllTextAsync(Path.Combine(root, "scripts", "gumps", "resurrect.lua"))
        );
        var templates =
            (await new GumpsLoader(new DirectoriesConfig(root, ["templates"])).LoadDataAsync()).Entities.ToArray();
        var options = new ScriptEngineOptions
        {
            ScriptsDirectory = _scripts.Path, MaxInstructionsPerResume = 20_000, MaxInstructionsPerChunk = 100_000,
            HookInterval = 100, WriteDefinitions = false
        };

        GumpScriptService? gumpScripts = null;
        _container.RegisterMoongateEventBus();
        _container.RegisterInstance<IGameLoopService>(_loop);
        _container.RegisterInstance<ITimerService>(_timers);
        _container.RegisterInstance<IItemService>(items);
        _container.RegisterInstance<ISessionService>(_fixture.Sessions);
        _container.RegisterInstance<IPacketSendService>(_fixture.Sender);
        _container.RegisterInstance<IWorldViewService>(_view);
        _container.RegisterInstance<IMobileService>(_fixture.Mobiles);
        _container.RegisterInstance<ISpeechService>(_speech);
        _container.RegisterInstance<ISectorService>(_fixture.Sectors);
        _container.RegisterInstance<ITooltipService>(TestTooltips.Create(items, _fixture.Mobiles));
        _container.RegisterInstance<ITeleportService>(
            new TeleportService(
                _fixture.Mobiles,
                _view,
                _fixture.Sessions,
                _fixture.Sender,
                _fixture.Sectors,
                new StubBankService()
            )
        );
        _container.RegisterInstance<IDeathService>(_death);
        _container.RegisterInstance<IEffectService>(_effects);
        _container.RegisterScriptEnum<EffectGraphicType>();
        _container.AddScriptModule<EffectModule>();
        _container.RegisterInstance<IGumpService>(_gumps);
        _container.RegisterInstance<IGumpTemplateService>(
            new GumpTemplateService(_gumps, new StubDataLoaderService().With(templates), _loop, _fixture.Sessions)
        );
        _container.RegisterDelegate<IGumpScriptService>(_ => gumpScripts!);
        _container.RegisterInstance(TestLocalization.With((2749, "You have moved too far away to use this.")));
        _container.AddScriptModule<LocalizationModule>();
        _container.Register<IItemHandlingService, ItemHandlingService>(Reuse.Singleton);
        _container.AddScriptModule<ItemModule>();
        _container.AddScriptModule<MobileModule>();
        _container.AddScriptModule<GumpModule>();
        _container.Resolve<IMoongateEventBus>()
            .Subscribe<ScriptErrorEvent>((evt, _) =>
                {
                    _errors.Add(evt);

                    return Task.CompletedTask;
                }
            );

        _engine = new(
            options,
            _container.Resolve<IScriptModuleRegistry>(),
            _container,
            _loop,
            _timers,
            new EventBusAdapter(_container)
        );
        await _engine.StartAsync();
        gumpScripts = new GumpScriptService(_engine, _loop, options);
        await gumpScripts.StartAsync();
        _itemScripts = new ItemScriptService(
            _engine,
            new ItemTemplateService(
                new StubDataLoaderService().With(new ItemTemplate { Id = "decoration_ankh", ScriptId = "ankh" })
            ),
            _loop,
            options
        );
        await _itemScripts.StartAsync();
    }

    [Fact]
    public void AGhostUsingAnAnkh_IsAskedWhetherItWantsToLive_AndTheLivingHaveNoFunctionToRun()
    {
        Die();

        _itemScripts.Run(_ankh, "on_ghost_use", 2L);

        Assert.Empty(_errors);
        Assert.Equal("resurrect", Assert.Single(_gumps.Opened).Gump.Id);
        Assert.Equal(ScriptResultKind.Missing, _itemScripts.Run(_ankh, "on_use", 2L).Kind);
    }

    [Fact]
    public void AGhostTooFarFromTheAnkh_IsToldItIsTooFar()
    {
        Die();
        _aria.Location = new Point3D(1340, 1997, 5);

        _itemScripts.Run(_ankh, "on_ghost_use", 2L);

        Assert.Empty(_errors);
        Assert.Empty(_gumps.Opened);
        Assert.Equal([(_aria, TooFarCliloc, "")], _speech.ToldClilocs);
    }

    [Fact]
    public void Continue_RaisesTheGhost_WithTheSoundAndTheSparkles()
    {
        Die();
        _itemScripts.Run(_ankh, "on_ghost_use", 2L);

        Answer(ContinueButton);

        Assert.Empty(_errors);
        Assert.Equal([_aria], _death.PlayersRaised);
        Assert.Equal((_aria, 0x214), Assert.Single(_speech.Sounds));
        Assert.Equal(0x376A, Assert.Single(_effects.On).Options.Graphic);
    }

    [Fact]
    public void Continue_AfterWalkingAway_RaisesNobody()
    {
        Die();
        _itemScripts.Run(_ankh, "on_ghost_use", 2L);
        _aria.Location = new Point3D(1340, 1997, 5);

        Answer(ContinueButton);

        Assert.Empty(_errors);
        Assert.Empty(_death.PlayersRaised);
        Assert.Equal([(_aria, TooFarCliloc, "")], _speech.ToldClilocs);
    }

    [Fact]
    public void Continue_OfAGhostAlreadyRaised_DoesNothing()
    {
        Die();
        _itemScripts.Run(_ankh, "on_ghost_use", 2L);
        _aria.Body = 0x0190;

        Answer(ContinueButton);

        Assert.Empty(_errors);
        Assert.Empty(_death.PlayersRaised);
    }

    private void Die()
    {
        _aria.AccountId = new Serial(0x42);
        _aria.Body = 0x0192;
    }

    private void Answer(int button)
    {
        _gumps.Opened[0]
            .Gump.OnResponse(
                _session,
                new GumpResponse { ButtonId = button, Switches = new HashSet<int>(), Texts = new Dictionary<int, string>() }
            );
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Moongate.slnx")))
        {
            directory = directory.Parent;
        }

        return directory!.FullName;
    }

    public async Task DisposeAsync()
    {
        _engine.Dispose();
        _container.Dispose();
        _scripts.Dispose();
        await _fixture.DisposeAsync();
    }
}
