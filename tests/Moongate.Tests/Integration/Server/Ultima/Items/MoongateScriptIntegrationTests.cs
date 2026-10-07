using DryIoc;
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
using Moongate.Server.Ultima.Data.Regions;
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
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Tests.TestSupport.Ultima.Tooltips;
using Moongate.Tests.TestSupport.Ultima.World;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Integration.Server.Ultima.Items;

/// <summary>
///     Runs the plain moongate script shipped in <c>moongate_root</c> with the real Lua engine: the delay, the
///     destination, the question asked when leaving a guarded place.
/// </summary>
public sealed class MoongateScriptIntegrationTests : IAsyncLifetime
{
    // The buttons answer in the order the script adds them.
    private const int OkayButton = 1;
    private const int CancelButton = 2;

    private const string TooFar = "That is too far away.";
    private const string Nowhere = "This moongate does not seem to go anywhere.";

    // Outside every region of the test.
    private static readonly Point3D Wilderness = new(1000, 1000, 0);
    private static readonly Point3D Covetous = new(2500, 500, 0);

    // Inside the guarded region of the test.
    private static readonly Point3D Britain = new(1496, 1628, 10);
    private static readonly Point3D BritainBank = new(1434, 1699, 2);

    private readonly TemporaryScriptsDirectory _scripts = new();
    private readonly Container _container = new();
    private readonly StubGameLoop _loop = new();
    private readonly RecordingTimerService _timers = new();
    private readonly RecordingGumpService _gumps = new();
    private readonly RecordingSpeechService _speech = new();
    private readonly RecordingWorldViewService _view = new();
    private readonly List<ScriptErrorEvent> _errors = [];

    private readonly ItemEntity _gate = new()
    {
        Id = new Serial(0x40000040), TemplateId = "moongate", ItemId = 0x0F6C, Amount = 1,
        Props = new() { ["teleport.x"] = 2500L, ["teleport.y"] = 500L, ["teleport.z"] = 0L }
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
        _items = TestItems.Create(_fixture.Sectors);
        Put(Wilderness);
        _items.Add([_gate]);
        _scripts.Write(
            "items/moongate.lua",
            await File.ReadAllTextAsync(Path.Combine(RepositoryRoot(), "moongate_root", "scripts", "items", "moongate.lua"))
        );
        var options = new ScriptEngineOptions
        {
            ScriptsDirectory = _scripts.Path, MaxInstructionsPerResume = 20_000, MaxInstructionsPerChunk = 100_000,
            HookInterval = 100, WriteDefinitions = false
        };

        GumpScriptService? gumpScripts = null;
        _container.RegisterMoongateEventBus();
        _container.RegisterInstance<IGameLoopService>(_loop);
        _container.RegisterInstance<ITimerService>(_timers);
        _container.RegisterInstance<IItemService>(_items);
        _container.RegisterInstance<ISessionService>(_fixture.Sessions);
        _container.RegisterInstance<IPacketSendService>(_fixture.Sender);
        _container.RegisterInstance<IWorldViewService>(_view);
        _container.RegisterInstance<IMobileService>(_fixture.Mobiles);
        _container.RegisterInstance<ISpeechService>(_speech);
        _container.RegisterInstance<ISectorService>(_fixture.Sectors);
        _container.RegisterInstance<IClockService>(new StubClockService());
        _container.RegisterInstance<ITooltipService>(TestTooltips.Create(_items, _fixture.Mobiles));
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
        _container.RegisterInstance<IRegionService>(
            new RegionService(
                new StubDataLoaderService().With(
                    new RegionContent
                    {
                        Map = MapType.Trammel, Name = "Britain", Guarded = true,
                        Areas = [new RegionAreaContent { X1 = 1400, Y1 = 1500, X2 = 1700, Y2 = 1800 }]
                    },
                    new RegionContent
                    {
                        Map = MapType.Trammel, Name = "Covetous",
                        Areas = [new RegionAreaContent { X1 = 2400, Y1 = 400, X2 = 2600, Y2 = 600 }]
                    }
                )
            )
        );
        _container.RegisterInstance<IGumpService>(_gumps);
        _container.RegisterInstance<IGumpTemplateService>(
            new GumpTemplateService(_gumps, new StubDataLoaderService().With<GumpTemplate>(), _loop, _fixture.Sessions)
        );
        _container.RegisterDelegate<IGumpScriptService>(_ => gumpScripts!);
        _container.RegisterInstance(TestLocalization.With((393, TooFar), (30114, Nowhere)));
        _container.AddScriptModule<LocalizationModule>();
        _container.Register<IItemHandlingService, ItemHandlingService>(Reuse.Singleton);
        _container.AddScriptModule<ItemModule>();
        _container.AddScriptModule<MobileModule>();
        _container.AddScriptModule<WorldModule>();
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
                new StubDataLoaderService().With(new ItemTemplate { Id = "moongate", ScriptId = "moongate" })
            ),
            _loop,
            options
        );
        await _itemScripts.StartAsync();
    }

    [Theory, InlineData("on_move_over"), InlineData("on_use")]
    public void TouchingTheGate_TakesThePlayerToTheDestination_ASecondLater(string function)
    {
        _itemScripts.Run(_gate, function, 2L);

        Assert.Equal(Wilderness, _aria.Location);
        Assert.Equal(TimeSpan.FromSeconds(1), Assert.Single(_timers.Timers).Interval);

        FireTimer();

        Assert.Empty(_errors);
        Assert.Equal((MapType.Trammel, Covetous), (_aria.Map, _aria.Location));
        Assert.Equal((_aria, 0x1FE), Assert.Single(_speech.Sounds));
        Assert.Empty(_gumps.Opened);
    }

    [Theory, InlineData(0L), InlineData("Felucca"), InlineData("felucca"), InlineData("0")]
    public void AGateWithAMap_TakesThePlayerToThatMap(object map)
    {
        _gate.Props!["teleport.map"] = map;

        _itemScripts.Run(_gate, "on_move_over", 2L);
        FireTimer();

        Assert.Empty(_errors);
        Assert.Equal((MapType.Felucca, Covetous), (_aria.Map, _aria.Location));
    }

    [Fact]
    public void UsingTheGateFromTheNextCell_Works()
    {
        _aria.Location = new Point3D(Wilderness.X + 1, Wilderness.Y, Wilderness.Z);

        _itemScripts.Run(_gate, "on_use", 2L);
        FireTimer();

        Assert.Equal(Covetous, _aria.Location);
    }

    [Fact]
    public void UsingTheGateFromFartherAway_TellsThePlayerAndStartsNothing()
    {
        var away = new Point3D(Wilderness.X + 2, Wilderness.Y, Wilderness.Z);
        _aria.Location = away;

        _itemScripts.Run(_gate, "on_use", 2L);

        Assert.Empty(_errors);
        Assert.Empty(_timers.Timers);
        Assert.Equal((_aria, TooFar), Assert.Single(_speech.Told));
        Assert.Equal(away, _aria.Location);
    }

    [Fact]
    public void WalkingOffTheGateDuringTheDelay_TeleportsNobody()
    {
        _itemScripts.Run(_gate, "on_move_over", 2L);
        var beside = new Point3D(Wilderness.X + 1, Wilderness.Y, Wilderness.Z);
        _aria.Location = beside;

        FireTimer();

        Assert.Empty(_errors);
        Assert.Equal(beside, _aria.Location);
        Assert.Empty(_speech.Told);
    }

    [Fact]
    public void AGateRemovedDuringTheDelay_TeleportsNobody()
    {
        _itemScripts.Run(_gate, "on_move_over", 2L);
        _items.Remove([_gate.Id]);

        FireTimer();

        Assert.Empty(_errors);
        Assert.Equal(Wilderness, _aria.Location);
    }

    [Theory, InlineData("teleport.x"), InlineData("teleport.z")]
    public void AGateWithoutADestination_SaysItGoesNowhere(string missing)
    {
        _gate.Props!.Remove(missing);

        _itemScripts.Run(_gate, "on_move_over", 2L);
        FireTimer();

        Assert.Empty(_errors);
        Assert.Equal(Wilderness, _aria.Location);
        Assert.Equal((_aria, Nowhere), Assert.Single(_speech.Told));
    }

    [Fact]
    public void AGateWithACoordinateThatIsNotWhole_SaysItGoesNowhere()
    {
        _gate.Props!["teleport.x"] = "12.5";

        _itemScripts.Run(_gate, "on_move_over", 2L);
        FireTimer();

        Assert.Empty(_errors);
        Assert.Equal(Wilderness, _aria.Location);
        Assert.Equal((_aria, Nowhere), Assert.Single(_speech.Told));
    }

    [Fact]
    public void AGateToAMapThatIsNotLoaded_SaysItGoesNowhere()
    {
        _gate.Props!["teleport.map"] = (long)MapType.Tokuno;

        _itemScripts.Run(_gate, "on_move_over", 2L);
        FireTimer();

        Assert.Empty(_errors);
        Assert.Equal((MapType.Trammel, Wilderness), (_aria.Map, _aria.Location));
        Assert.Equal((_aria, Nowhere), Assert.Single(_speech.Told));
        Assert.Empty(_speech.Sounds);
    }

    [Fact]
    public void TouchingTheGateAgainDuringTheDelay_StartsNothing_AndTheGateWorksAgainAfterwards()
    {
        _aria.Location = new Point3D(Wilderness.X + 1, Wilderness.Y, Wilderness.Z);
        _itemScripts.Run(_gate, "on_use", 2L);
        _itemScripts.Run(_gate, "on_use", 2L);
        _aria.Location = new Point3D(Wilderness.X + 3, Wilderness.Y, Wilderness.Z);
        FireTimer();

        _aria.Location = Wilderness;
        _itemScripts.Run(_gate, "on_move_over", 2L);
        FireTimer();

        Assert.Empty(_errors);
        Assert.Equal(Covetous, _aria.Location);
    }

    [Theory, InlineData("Atlantis"), InlineData(9L), InlineData(true)]
    public void AGateWithAMapThatDoesNotExist_SaysItGoesNowhere(object map)
    {
        _gate.Props!["teleport.map"] = map;

        _itemScripts.Run(_gate, "on_move_over", 2L);
        FireTimer();

        Assert.Empty(_errors);
        Assert.Equal(Wilderness, _aria.Location);
        Assert.Equal((_aria, Nowhere), Assert.Single(_speech.Told));
    }

    [Fact]
    public void LeavingAGuardedPlaceForOneThatIsNot_AsksFirst_WithTheWarningSound()
    {
        Put(Britain);

        _itemScripts.Run(_gate, "on_move_over", 2L);
        FireTimer();

        Assert.Empty(_errors);
        Assert.Equal(Britain, _aria.Location);
        var gump = Assert.Single(_gumps.Opened).Gump;
        Assert.Equal("moongate_warning", gump.Id);
        var layout = gump.Layout.Build().Layout;
        Assert.All(new[] { 1062051, 1062049, 1011036, 1011012 }, cliloc => Assert.Contains(cliloc.ToString(), layout));
        Assert.Equal((_aria, 0x20E), Assert.Single(_speech.Sounds));
    }

    [Fact]
    public void Okay_TakesThePlayerThere()
    {
        Put(Britain);
        _itemScripts.Run(_gate, "on_move_over", 2L);
        FireTimer();
        _speech.Sounds.Clear();

        Answer(OkayButton);

        Assert.Empty(_errors);
        Assert.Equal(Covetous, _aria.Location);
        Assert.Equal((_aria, 0x1FE), Assert.Single(_speech.Sounds));
    }

    [Fact]
    public void Cancel_DoesNothing()
    {
        Put(Britain);
        _itemScripts.Run(_gate, "on_move_over", 2L);
        FireTimer();

        Answer(CancelButton);

        Assert.Empty(_errors);
        Assert.Equal(Britain, _aria.Location);
    }

    [Fact]
    public void OkayAfterWalkingAway_TellsThePlayerAndTeleportsNobody()
    {
        Put(Britain);
        _itemScripts.Run(_gate, "on_move_over", 2L);
        FireTimer();
        var away = new Point3D(Britain.X + 3, Britain.Y, Britain.Z);
        _aria.Location = away;

        Answer(OkayButton);

        Assert.Empty(_errors);
        Assert.Equal(away, _aria.Location);
        Assert.Equal((_aria, TooFar), Assert.Single(_speech.Told));
    }

    [Fact]
    public void BetweenTwoGuardedPlaces_NothingIsAsked()
    {
        Put(Britain);
        _gate.Props!["teleport.x"] = (long)BritainBank.X;
        _gate.Props["teleport.y"] = (long)BritainBank.Y;
        _gate.Props["teleport.z"] = (long)BritainBank.Z;

        _itemScripts.Run(_gate, "on_move_over", 2L);
        FireTimer();

        Assert.Empty(_errors);
        Assert.Empty(_gumps.Opened);
        Assert.Equal(BritainBank, _aria.Location);
    }

    [Fact]
    public void AGuardedPlaceOfAnotherMap_IsNotTheSamePlace()
    {
        // Britain is guarded on Trammel only in this test: the same spot on Felucca is not.
        Put(Britain);
        _gate.Props!["teleport.x"] = (long)BritainBank.X;
        _gate.Props["teleport.y"] = (long)BritainBank.Y;
        _gate.Props["teleport.z"] = (long)BritainBank.Z;
        _gate.Props["teleport.map"] = (long)MapType.Felucca;

        _itemScripts.Run(_gate, "on_move_over", 2L);
        FireTimer();

        Assert.Empty(_errors);
        Assert.Single(_gumps.Opened);
        Assert.Equal(Britain, _aria.Location);
    }

    // The player and the gate, on Trammel.
    private void Put(Point3D location)
    {
        _aria.Map = MapType.Trammel;
        _aria.Location = location;
        _gate.PlaceOnGround(MapType.Trammel, location);
    }

    private void FireTimer()
    {
        _timers.Fire(Assert.Single(_timers.Timers).Id);
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
