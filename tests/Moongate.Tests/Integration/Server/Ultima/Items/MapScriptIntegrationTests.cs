using DryIoc;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Scripting.Data.Config;
using Moongate.Scripting.Data.Events;
using Moongate.Scripting.Extensions.Scripts;
using Moongate.Scripting.Interfaces;
using Moongate.Scripting.Services;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Events;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.MapItems;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Modules;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Scripting;
using Moongate.Tests.TestSupport.Ultima.Bank;
using Moongate.Tests.TestSupport.Ultima.Death;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Tests.TestSupport.Ultima.Tooltips;
using Moongate.Tests.TestSupport.Ultima.World;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Integration.Server.Ultima.Items;

/// <summary>
///     Runs the shipped <c>scripts/items/map_item.lua</c> with the real Lua engine, the map module and the map service: a
///     preset map opens on its area, from the backpack or the ground within 2 tiles.
/// </summary>
public sealed class MapScriptIntegrationTests : IAsyncLifetime
{
    private const int TooFarCliloc = 500446;

    private static readonly Point3D Spot = new(1400, 1600, 0);

    private readonly TemporaryScriptsDirectory _scripts = new();
    private readonly Container _container = new();
    private readonly StubGameLoop _loop = new();
    private readonly RecordingTimerService _timers = new();
    private readonly RecordingSpeechService _speech = new();
    private readonly RecordingWorldViewService _view = new();
    private readonly List<ScriptErrorEvent> _errors = [];

    private readonly ItemEntity _map = new()
    {
        Id = new Serial(0x40000010), TemplateId = "britainmap", ItemId = 0x14EC, Amount = 1
    };

    private BroadcastFixture _fixture = null!;
    private LuaScriptEngineService _engine = null!;
    private ItemScriptService _itemScripts = null!;
    private MobileEntity _aria = null!;
    private ItemService _items = null!;

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        await _fixture.AddAsync(2);
        Assert.True(_fixture.Mobiles.TryGet(new Serial(2), out _aria!));
        _aria.Map = MapType.Trammel;
        _aria.Location = Spot;
        _items = TestItems.Create(_fixture.Sectors);
        _map.PlaceOnGround(MapType.Trammel, new Point3D(1402, 1600, 0));
        _items.Add([_map]);

        var root = Path.Combine(RepositoryRoot(), "moongate_root");
        _scripts.Write("items/map_item.lua", await File.ReadAllTextAsync(Path.Combine(root, "scripts", "items", "map_item.lua")));
        var options = new ScriptEngineOptions
        {
            ScriptsDirectory = _scripts.Path, MaxInstructionsPerResume = 20_000, MaxInstructionsPerChunk = 100_000,
            HookInterval = 100, WriteDefinitions = false
        };
        var templates = new ItemTemplateService(
            new StubDataLoaderService().With(
                new ItemTemplate
                {
                    Id = "britainmap", ItemId = new Serial(0x14EC), ScriptId = "map_item",
                    Tags = new()
                    {
                        ["map_x1"] = "1092", ["map_y1"] = "1396", ["map_x2"] = "1736", ["map_y2"] = "1924",
                        ["map_width"] = "200", ["map_height"] = "200", ["map_facet"] = "0"
                    }
                }
            )
        );

        _container.RegisterMoongateEventBus();
        _container.RegisterInstance<IGameLoopService>(_loop);
        _container.RegisterInstance<ITimerService>(_timers);
        _container.RegisterInstance<IItemService>(_items);
        _container.RegisterInstance<IItemTemplateService>(templates);
        _container.RegisterInstance<ISessionService>(_fixture.Sessions);
        _container.RegisterInstance<IPacketSendService>(_fixture.Sender);
        _container.RegisterInstance<IWorldViewService>(_view);
        _container.RegisterInstance<IMobileService>(_fixture.Mobiles);
        _container.RegisterInstance<ISpeechService>(_speech);
        _container.RegisterInstance<ISectorService>(_fixture.Sectors);
        _container.RegisterInstance<ITooltipService>(TestTooltips.Create(_items, _fixture.Mobiles));
        _container.RegisterInstance<ITeleportService>(
            new TeleportService(_fixture.Mobiles, _view, _fixture.Sessions, _fixture.Sender, _fixture.Sectors, new StubBankService())
        );
        _container.RegisterInstance<IDeathService>(new StubDeathService());
        _container.Register<IItemHandlingService, ItemHandlingService>(Reuse.Singleton);
        _container.Register<IMapDisplayService, MapDisplayService>(Reuse.Singleton);
        _container.AddScriptModule<ItemModule>();
        _container.AddScriptModule<MobileModule>();
        _container.AddScriptModule<MapModule>();
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
        _itemScripts = new(_engine, templates, _loop, options);
        await _itemScripts.StartAsync();
    }

    [Fact]
    public void APresetMap_OpensOnItsArea()
    {
        _itemScripts.Run(_map, "on_use", 2L);

        Assert.Empty(_errors);
        var details = Assert.Single(_fixture.Sender.Sent.OfType<MapDetailsPacket>());
        Assert.Equal(new MapArea(1092, 1396, 1736, 1924, 200, 200, 0), details.Area);
    }

    [Fact]
    public void AMapThreeTilesAway_IsTooFar()
    {
        _aria.Location = new Point3D(1405, 1600, 0);

        _itemScripts.Run(_map, "on_use", 2L);

        Assert.Empty(_errors);
        Assert.Empty(_fixture.Sender.Sent.OfType<MapDetailsPacket>());
        Assert.Equal([(_aria, TooFarCliloc, "")], _speech.ToldClilocs);
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
