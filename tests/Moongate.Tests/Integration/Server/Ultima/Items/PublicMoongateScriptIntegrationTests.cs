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
using Moongate.Server.Ultima.Data.Templates.Gumps;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Modules;
using Moongate.Server.Ultima.Packets.General;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Localization;
using Moongate.Tests.TestSupport.Scripting;
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
///     Runs the public moongate script shipped in <c>moongate_root</c> with the real Lua engine and the real gump
///     module: the gump it builds and what its buttons do.
/// </summary>
public sealed class PublicMoongateScriptIntegrationTests : IAsyncLifetime
{
    // The buttons answer in the order the script adds them: cancel, then the cities page by page.
    private const int CancelButton = 1;
    private const int TrammelBritainButton = 2;
    private const int MoonglowButton = 3;
    private const int FeluccaBritainButton = 4;

    private static readonly Point3D TrammelBritain = new(1336, 1997, 5);
    private static readonly Point3D Moonglow = new(4467, 1283, 5);

    private readonly TemporaryScriptsDirectory _scripts = new();
    private readonly Container _container = new();
    private readonly StubGameLoop _loop = new();
    private readonly RecordingTimerService _timers = new();
    private readonly RecordingGumpService _gumps = new();
    private readonly RecordingSpeechService _speech = new();
    private readonly RecordingWorldViewService _view = new();
    private readonly StubPublicMoongateService _moongates = new();
    private readonly List<ScriptErrorEvent> _errors = [];
    private readonly ItemEntity _gate = new()
    {
        Id = new Serial(0x40000040), TemplateId = "decoration_public_moongate", ItemId = 0x0F6C, Amount = 1
    };

    private BroadcastFixture _fixture = null!;
    private GameSession _session = null!;
    private LuaScriptEngineService _engine = null!;
    private ItemScriptService _itemScripts = null!;
    private MobileEntity _aria = null!;

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        _session = await _fixture.AddAsync(2);
        Assert.True(_fixture.Mobiles.TryGet(new Serial(2), out _aria!));
        _aria.Map = MapType.Trammel;
        _aria.Location = TrammelBritain;
        var items = TestItems.Create(_fixture.Sectors);
        _gate.PlaceOnGround(MapType.Trammel, TrammelBritain);
        items.Add([_gate]);
        _moongates.Facets.Add(
            new()
            {
                Map = MapType.Felucca, Cliloc = 1012001, SelectedCliloc = 1012013,
                Destination = [new() { Name = "Britain", Cliloc = 1012004, Location = TrammelBritain }]
            }
        );
        _moongates.Facets.Add(
            new()
            {
                Map = MapType.Trammel, Cliloc = 1012000, SelectedCliloc = 1012012,
                Destination =
                [
                    new() { Name = "Britain", Cliloc = 1012004, Location = TrammelBritain },
                    new() { Name = "Moonglow", Cliloc = 1012003, Location = Moonglow }
                ]
            }
        );
        _scripts.Write(
            "items/public_moongate.lua",
            await File.ReadAllTextAsync(Path.Combine(RepositoryRoot(), "moongate_root", "scripts", "items", "public_moongate.lua"))
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
        _container.RegisterInstance<IItemService>(items);
        _container.RegisterInstance<ISessionService>(_fixture.Sessions);
        _container.RegisterInstance<IPacketSendService>(_fixture.Sender);
        _container.RegisterInstance<IWorldViewService>(_view);
        _container.RegisterInstance<IMobileService>(_fixture.Mobiles);
        _container.RegisterInstance<ISpeechService>(_speech);
        _container.RegisterInstance<ISectorService>(_fixture.Sectors);
        _container.RegisterInstance<ITooltipService>(TestTooltips.Create(items, _fixture.Mobiles));
        _container.RegisterInstance<ITeleportService>(
            new TeleportService(_fixture.Mobiles, _view, _fixture.Sessions, _fixture.Sender, _fixture.Sectors)
        );
        _container.RegisterInstance<IPublicMoongateService>(_moongates);
        _container.RegisterInstance<IGumpService>(_gumps);
        _container.RegisterInstance<IGumpTemplateService>(
            new GumpTemplateService(_gumps, new StubDataLoaderService().With<GumpTemplate>(), _loop, _fixture.Sessions)
        );
        _container.RegisterDelegate<IGumpScriptService>(_ => gumpScripts!);
        _container.RegisterInstance(TestLocalization.With((2749, "You have moved too far away to use this.")));
        _container.AddScriptModule<LocalizationModule>();
        _container.AddScriptModule<ItemModule>();
        _container.AddScriptModule<MobileModule>();
        _container.AddScriptModule<GumpModule>();
        _container.AddScriptModule<MoongatesModule>();
        _container.Resolve<IMoongateEventBus>()
                  .Subscribe<ScriptErrorEvent>((evt, _) =>
                      {
                          _errors.Add(evt);

                          return Task.CompletedTask;
                      }
                  );

        _engine = new(options, _container.Resolve<IScriptModuleRegistry>(), _container, _loop, _timers, new EventBusAdapter(_container));
        await _engine.StartAsync();
        gumpScripts = new GumpScriptService(_engine, _loop, options);
        await gumpScripts.StartAsync();
        _itemScripts = new ItemScriptService(
            _engine,
            new ItemTemplateService(
                new StubDataLoaderService().With(new ItemTemplate { Id = "decoration_public_moongate", ScriptId = "public_moongate" })
            ),
            _loop,
            options
        );
        await _itemScripts.StartAsync();
    }

    public async Task DisposeAsync()
    {
        _engine.Dispose();
        _container.Dispose();
        _scripts.Dispose();
        await _fixture.DisposeAsync();
    }

    [Theory, InlineData("on_move_over"), InlineData("on_use")]
    public void SteppingOnTheGateOrUsingIt_OpensTheGumpOfTheDestinations_WithTheOpeningSound(string function)
    {
        _itemScripts.Run(_gate, function, 2L);

        Assert.Empty(_errors);
        var gump = Assert.Single(_gumps.Opened).Gump;
        Assert.Equal("public_moongate", gump.Id);
        var layout = gump.Layout.Build().Layout;
        // The title, both map names and every city, as client texts.
        Assert.All(
            new[] { 1012011, 1012000, 1012001, 1012012, 1012013, 1012004, 1012003 },
            cliloc => Assert.Contains(cliloc.ToString(), layout)
        );
        Assert.Equal((_aria, 0x20E), Assert.Single(_speech.Sounds));
    }

    [Fact]
    public void ThePageOfThePlayersMap_OpensFirst()
    {
        _itemScripts.Run(_gate, "on_move_over", 2L);

        // Trammel is the second facet of the file, yet its cities are on page 1: Moonglow comes before the page of Felucca.
        var layout = Assert.Single(_gumps.Opened).Gump.Layout.Build().Layout;
        Assert.True(layout.IndexOf("1012003", StringComparison.Ordinal) < layout.IndexOf("{ page 2 }", StringComparison.Ordinal));
    }

    [Fact]
    public void ChoosingACity_TakesThePlayerThere_WithTheArrivalSound()
    {
        _itemScripts.Run(_gate, "on_move_over", 2L);
        _speech.Sounds.Clear();

        Answer(MoonglowButton);

        Assert.Empty(_errors);
        Assert.Equal((MapType.Trammel, Moonglow), (_aria.Map, _aria.Location));
        Assert.Equal((_aria, 0x1FE), Assert.Single(_speech.Sounds));
    }

    [Fact]
    public void ChoosingACityOfAnotherMap_ChangesTheMap()
    {
        _itemScripts.Run(_gate, "on_move_over", 2L);

        Answer(FeluccaBritainButton);

        Assert.Empty(_errors);
        Assert.Equal((MapType.Felucca, TrammelBritain), (_aria.Map, _aria.Location));
    }

    [Fact]
    public void ChoosingTheCityOfThisVeryGate_DoesNothing()
    {
        _itemScripts.Run(_gate, "on_move_over", 2L);
        _speech.Sounds.Clear();

        Answer(TrammelBritainButton);

        Assert.Empty(_errors);
        Assert.Equal((MapType.Trammel, TrammelBritain), (_aria.Map, _aria.Location));
        Assert.Empty(_speech.Sounds);
    }

    [Fact]
    public void ChoosingAfterWalkingAway_TellsThePlayerAndTeleportsNobody()
    {
        _itemScripts.Run(_gate, "on_move_over", 2L);
        _aria.Location = new Point3D(1340, 1997, 5);
        _speech.Sounds.Clear();

        Answer(MoonglowButton);

        Assert.Empty(_errors);
        Assert.Equal(new Point3D(1340, 1997, 5), _aria.Location);
        Assert.Empty(_speech.Sounds);
        Assert.Equal(
            "You have moved too far away to use this.",
            Assert.Single(_fixture.Sender.Sent.OfType<UnicodeSpeechMessagePacket>()).Text
        );
    }

    [Fact]
    public void Cancel_DoesNothing()
    {
        _itemScripts.Run(_gate, "on_move_over", 2L);

        Answer(CancelButton);

        Assert.Empty(_errors);
        Assert.Equal(TrammelBritain, _aria.Location);
    }

    [Fact]
    public void APlayerTooFarFromTheGate_GetsNoGump()
    {
        _aria.Location = new Point3D(1338, 1997, 5);

        _itemScripts.Run(_gate, "on_use", 2L);

        Assert.Empty(_errors);
        Assert.Empty(_gumps.Opened);
    }

    [Fact]
    public void WithoutMoongates_NoGumpOpens()
    {
        _moongates.Facets.Clear();

        _itemScripts.Run(_gate, "on_move_over", 2L);

        Assert.Empty(_errors);
        Assert.Empty(_gumps.Opened);
    }

    private void Answer(int button)
    {
        _gumps.Opened[0].Gump.OnResponse(_session, new GumpResponse { ButtonId = button, Switches = new HashSet<int>(), Texts = new Dictionary<int, string>() });
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
}
