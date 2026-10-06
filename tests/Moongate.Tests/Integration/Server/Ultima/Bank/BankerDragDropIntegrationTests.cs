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
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.Containers;
using Moongate.Server.Ultima.Data.Internal.Items;
using Moongate.Server.Ultima.Data.Items;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Data.Templates.Mobiles;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Handlers.Items;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Modules;
using Moongate.Server.Ultima.Packets.General;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Types.Bank;
using Moongate.Server.Ultima.Types.Speech;
using Moongate.Tests.TestSupport.Scripting;
using Moongate.Tests.TestSupport.Timing;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Mobiles;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Tests.TestSupport.Ultima.Tiles;
using Moongate.Tests.TestSupport.Ultima.Tooltips;
using Moongate.Tests.TestSupport.Ultima.Weight;
using Moongate.Tests.TestSupport.Ultima.World;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Integration.Server.Ultima.Bank;

/// <summary>
///     A drop on a banker from the packet to the bank box: the real drop handler, the shipped <c>banker.lua</c>, the
///     real bank module and the real bank service.
/// </summary>
public sealed class BankerDragDropIntegrationTests : IAsyncLifetime
{
    private const int GoldGraphic = 0x0EED;

    private static readonly Serial Banker = new(0x100);

    private readonly TemporaryScriptsDirectory _scripts = new();
    private readonly Container _container = new();
    private readonly ItemService _items = TestItems.Create();
    private readonly RecordingSpeechService _speech = new();
    private readonly RecordingWorldViewService _view = new();
    private readonly StubItemSerialPool _serials = new();
    private readonly List<ScriptErrorEvent> _errors = [];

    private readonly FakeTileDataService _tiles = new FakeTileDataService()
        .Item(GoldGraphic, TileFlagType.Generic, 0)
        .Item(0x0E75, TileFlagType.Container, 0)
        .Item(0x0E7C, TileFlagType.Container, 0);

    private readonly ItemEntity _backpack = new()
        { Id = new Serial(0x40000001), TemplateId = "backpack", ItemId = 0x0E75, Amount = 1 };

    private readonly ItemEntity _box = new()
        { Id = new Serial(0x40000002), TemplateId = BankService.BankTemplate, ItemId = 0x0E7C, Amount = 1 };

    private readonly ItemEntity _gold = new()
        { Id = new Serial(0x40000003), TemplateId = "gold", ItemId = GoldGraphic, Amount = 1250 };

    private BroadcastFixture _fixture = null!;
    private GameSession _session = null!;
    private MobileEntity _aria = null!;
    private LuaScriptEngineService _engine = null!;
    private DropRequestPacketHandler _handler = null!;
    private BankService _bank = null!;

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        _session = await _fixture.AddAsync(2);
        Assert.True(_fixture.Mobiles.TryGet(new Serial(2), out var aria));
        _aria = aria;
        _aria.AccountId = new Serial(1002);
        _aria.Location = new Point3D(1600, 1600, 0);
        _fixture.Mobiles.EnterWorld(
            new()
            {
                Id = Banker, Name = "a banker", TemplateId = "banker", Map = MapType.Trammel,
                Location = new Point3D(1602, 1600, 0)
            }
        );

        _backpack.Equip(_aria.Id, LayerType.Backpack);
        _box.Equip(_aria.Id, LayerType.Bank);
        _gold.PutInContainer(_backpack.Id, new Point2D(44, 65));
        _items.Add([_backpack, _box, _gold]);

        for (uint index = 0; index < 8; index++)
        {
            _serials.Serials.Enqueue(new Serial(0x40002000 + index));
        }

        var itemTemplates = new ItemTemplateService(
            new StubDataLoaderService().With(
                new ItemTemplate { Id = BankService.BankTemplate, ItemId = new Serial(0x0E7C), Movable = false },
                new ItemTemplate { Id = "gold", ItemId = new Serial(GoldGraphic), Weight = 0.02m },
                new ItemTemplate { Id = "backpack", ItemId = new Serial(0x0E75) },
                new ItemTemplate { Id = BankService.CheckTemplate, ItemId = new Serial(0x14F0) }
            )
        );
        var layouts = new ContainerLayoutService(
            new StubDataLoaderService().With(
                new ContainerContent { Name = "backpack", Gump = 0x003C, Items = [0x0E75, 0x0E7C], Default = true }
            )
        );
        var bankConfig = new BankConfig();
        var itemsConfig = new ItemsConfig { GoldTemplate = "gold", BackpackTemplate = "backpack" };
        var capacity = new ContainerCapacityService(_items, itemTemplates, bankConfig);
        var tooltips = TestTooltips.Create(_items, _fixture.Mobiles);
        var factory = new FakeItemFactoryService(itemTemplates, _tiles);
        var handling = new ItemHandlingService(
            _items,
            _fixture.Sessions,
            _fixture.Sender,
            _view,
            tooltips,
            factory,
            _serials,
            layouts,
            capacity
        );
        _bank = new(
            _items,
            factory,
            _fixture.Sessions,
            _fixture.Mobiles,
            _fixture.Sender,
            tooltips,
            layouts,
            _fixture.Network.Loop,
            handling,
            capacity,
            new StubWeightService(),
            itemsConfig,
            bankConfig
        );

        _container.RegisterMoongateEventBus();
        _container.RegisterInstance<IGameLoopService>(_fixture.Network.Loop);
        _container.RegisterInstance<ITimerService>(new RecordingTimerService());
        _container.RegisterInstance<IMobileService>(_fixture.Mobiles);
        _container.RegisterInstance<ISpeechService>(_speech);
        _container.RegisterInstance<IWorldViewService>(_view);
        _container.RegisterInstance<IItemService>(_items);
        _container.RegisterInstance<IBankService>(_bank);
        var mobileTemplates = new MobileTemplateService(
            new StubDataLoaderService().With(new MobileTemplate { Id = "banker", ScriptId = "banker" })
        );
        _container.RegisterInstance<IMobileTemplateService>(mobileTemplates);
        _container.RegisterInstance<ITeleportService>(new RecordingTeleportService());
        _container.RegisterInstance<ICrimeService>(new RecordingCrimeService());
        _container.AddScriptModule<NpcModule>();
        _container.AddScriptModule<BankModule>();
        _container.AddScriptModule<MobileModule>();
        _container.RegisterScriptEnum<BankResultType>();
        _container.RegisterScriptEnum<SpeechKeywordType>();
        _container.Resolve<IMoongateEventBus>()
            .Subscribe<ScriptErrorEvent>((evt, _) =>
                {
                    _errors.Add(evt);

                    return Task.CompletedTask;
                }
            );

        _scripts.Write("mobiles/banker.lua", File.ReadAllText(ShippedScript("mobiles/banker.lua")));
        _scripts.Write("common/numbers.lua", File.ReadAllText(ShippedScript("common/numbers.lua")));
        var options = new ScriptEngineOptions
        {
            ScriptsDirectory = _scripts.Path,
            MaxInstructionsPerResume = 20_000,
            MaxInstructionsPerChunk = 100_000,
            HookInterval = 100,
            WriteDefinitions = false
        };
        _engine = new(
            options,
            _container.Resolve<IScriptModuleRegistry>(),
            _container,
            _fixture.Network.Loop,
            _container.Resolve<ITimerService>(),
            new EventBusAdapter(_container)
        );
        await _engine.StartAsync();
        var npcScripts = new NpcScriptService(_engine, mobileTemplates, _fixture.Network.Loop, options);
        await npcScripts.StartAsync();

        _handler = new(
            _items,
            _fixture.Mobiles,
            _view,
            _tiles,
            layouts,
            _fixture.Sender,
            tooltips,
            bank: _bank,
            npcScripts: npcScripts
        );
    }

    [Fact]
    public async Task GoldDroppedOnTheBanker_IsInTheBank_AndTheBankerSaysHowMuch()
    {
        await DropOnTheBankerAsync(_gold);

        Assert.Empty(_errors);
        Assert.Equal((Banker.Value, 1042763, "1,250"), Said());
        Assert.Equal(1250, _bank.Balance(_aria));
        Assert.Empty(_items.GetContents(_backpack.Id));
        Assert.Contains(_fixture.Sender.Sent.OfType<RemoveEntityPacket>(), packet => packet.Serial == _gold.Id);
        Assert.Null(_session.Get(ItemSessionKeys.Held));
    }

    [Fact]
    public async Task WhatIsNotMoney_ComesBack_AndTheBankerIsNotInterested()
    {
        var sword = new ItemEntity { Id = new Serial(0x40000004), TemplateId = "sword", ItemId = 0x0F5E, Amount = 1 };
        sword.PutInContainer(_backpack.Id, new Point2D(80, 80));
        _items.Add([sword]);

        await DropOnTheBankerAsync(sword);

        Assert.Empty(_errors);
        Assert.Equal((Banker.Value, 501550, ""), Said());
        Assert.Equal(_backpack.Id, sword.ContainerId);
    }

    private (uint Speaker, int Cliloc, string Arguments) Said()
    {
        var (speaker, cliloc, arguments) = Assert.Single(_speech.SaidClilocs);

        return (speaker.Id.Value, cliloc, arguments);
    }

    private Task DropOnTheBankerAsync(ItemEntity item)
    {
        var packet = new DropRequestPacket { Item = item.Id, X = -1, Y = -1, Z = 0, GridIndex = 0, Destination = Banker };

        return _fixture.Network.ExecuteOnLoopAsync(() =>
            {
                _session.Set(ItemSessionKeys.Held, new HeldItem(item.Id));
                _handler.Handle(_session, packet);
            }
        );
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

    public async Task DisposeAsync()
    {
        _engine.Dispose();
        _container.Dispose();
        _scripts.Dispose();
        await _fixture.DisposeAsync();
    }
}
