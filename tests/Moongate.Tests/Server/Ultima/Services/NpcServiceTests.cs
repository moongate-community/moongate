using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.Mobiles;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.Support.Sessions;
using Moongate.Tests.TestSupport.Persistence;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Mobiles;
using Moongate.Tests.TestSupport.Ultima.Npcs;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Tests.TestSupport.Ultima.Sectors;
using Moongate.Tests.TestSupport.Ultima.World;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class NpcServiceTests : IAsyncDisposable
{
    private readonly RecordingWorldViewService _view = new();
    private readonly RecordingNpcTickService _ticks = new();
    private readonly RecordingNpcScriptService _scripts = new();
    private readonly RecordingItemScriptService _itemScripts = new();
    private readonly SectorService _sectors;
    private readonly MobileService _mobiles;
    private readonly ItemService _items;
    private readonly MobileEntity _orc = new() { Id = new(0x00000100), Name = "Orc", TemplateId = "orc", Body = 0x0011 };
    private readonly ItemEntity _shirt = new() { Id = new(0x40000100), TemplateId = "shirt", ItemId = 0x1517, Amount = 1 };
    private readonly ItemEntity _backpack = new() { Id = new(0x40000101), TemplateId = "backpack", ItemId = 0x0E75, Amount = 1 };
    private readonly ItemEntity _gold = new() { Id = new(0x40000102), TemplateId = "gold", ItemId = 0x0EED, Amount = 50 };
    private readonly StubMobileFactoryService _factory;

    private SessionFixture? _fixture;

    public NpcServiceTests()
    {
        var sectors = TestSectors.Create(ticks: _ticks);
        _sectors = sectors;
        _mobiles = new(new StubMovementService(), sectors, new NpcSenseService(_scripts, sectors, new NpcsConfig()));
        _items = TestItems.Create(sectors);
        _shirt.Equip(_orc.Id, LayerType.Shirt);
        _backpack.Equip(_orc.Id, LayerType.Backpack);
        _gold.PutInContainer(_backpack.Id, new Point2D(44, 65));
        _factory = new() { Spawned = new SpawnedMobile(_orc, [_shirt], _backpack, [_gold]) };
    }

    [Fact]
    public async Task SpawnAsync_PutsTheNpcAndItsItemsInTheWorldAndShowsIt()
    {
        var npcs = await CreateAsync();

        var npc = await npcs.SpawnAsync("orc", MapType.Trammel, new Point3D(1496, 1628, 0));

        Assert.Same(_orc, npc);
        Assert.Equal(("orc", MapType.Trammel, new Point3D(1496, 1628, 0)), Assert.Single(_factory.Spawns));
        Assert.True(_mobiles.IsInWorld(_orc.Id));
        Assert.All([_shirt.Id, _backpack.Id, _gold.Id], serial => Assert.True(_items.TryGet(serial, out _)));
        Assert.Equal(["MobileAppeared 256"], _view.Calls);
    }

    [Fact]
    public async Task SpawnAsync_QueuesOnSpawnOfItsScript()
    {
        var npcs = await CreateAsync();

        await npcs.SpawnAsync("orc", MapType.Trammel, new Point3D(1496, 1628, 0));

        Assert.Equal(["Queue 256 on_spawn"], _scripts.Calls);
    }

    [Fact]
    public async Task SpawnAsync_NextToAPlayer_QueuesOnSpawnBeforeItSensesThePlayer()
    {
        // A script sets itself up in on_spawn: nothing else of it may run before.
        var npcs = await CreateAsync();
        _sectors.Add(new MobileEntity { Id = new(2), Name = "Aria", AccountId = new Serial(0x42), Map = MapType.Trammel, Location = new Point3D(1496, 1630, 0) });

        await npcs.SpawnAsync("orc", MapType.Trammel, new Point3D(1496, 1628, 0));

        Assert.Equal(["Queue 256 on_spawn", "Queue 256 on_mobile_in_range 2"], _scripts.Calls);
    }

    [Fact]
    public async Task SpawnAsync_QueuesOnCreateOfTheNewItemsWithAScript()
    {
        _itemScripts.Scripted.Add("shirt");
        _itemScripts.Scripted.Add("gold");
        var npcs = await CreateAsync();

        await npcs.SpawnAsync("orc", MapType.Trammel, new Point3D(1496, 1628, 0));

        Assert.Equal(["0x40000100 on_create", "0x40000102 on_create"], _itemScripts.Queued);
    }

    [Fact]
    public async Task StartAsync_LoadingTheSavedNpcs_IsNoSpawn()
    {
        var npcs = await CreateAsync();

        await npcs.StartAsync();

        Assert.Empty(_scripts.Calls);
        Assert.Empty(_itemScripts.Queued);
    }

    [Fact]
    public async Task Remove_OnTheGameLoop_TakesTheNpcOffAtOnce_AndRefusesWhatIsNoNpc()
    {
        var npcs = await CreateAsync();
        await npcs.SpawnAsync("orc", MapType.Trammel, new Point3D(1496, 1628, 0));
        var removed = new List<bool>();

        await _fixture.ExecuteOnLoopAsync(
            () =>
            {
                removed.Add(npcs.Remove(_orc.Id));
                removed.Add(npcs.Remove(_orc.Id));
                removed.Add(npcs.Remove(new Serial(0x00FFFFFF)));
            }
        );

        Assert.Equal([true, false, false], removed);
        Assert.False(_mobiles.IsInWorld(_orc.Id));
        Assert.All([_shirt.Id, _backpack.Id, _gold.Id], serial => Assert.False(_items.TryGet(serial, out _)));
    }

    [Fact]
    public async Task RemoveAsync_TakesTheNpcOffTheScreensAndQueuesItsDeletion()
    {
        var npcs = await CreateAsync();
        await npcs.SpawnAsync("orc", MapType.Trammel, new Point3D(1496, 1628, 0));

        Assert.True(await npcs.RemoveAsync(_orc.Id));

        Assert.Equal("Left 256", _view.Calls.Last());
        Assert.False(_mobiles.IsInWorld(_orc.Id));
        Assert.All([_shirt.Id, _backpack.Id, _gold.Id], serial => Assert.False(_items.TryGet(serial, out _)));
        Assert.Equal([_orc.Id], _mobiles.Capture());
    }

    [Fact]
    public async Task RemoveAsync_AnNpcNextToAPlayer_StopsItsThinks()
    {
        var npcs = await CreateAsync();
        _sectors.Add(new MobileEntity { Id = new(2), Name = "Aria", AccountId = new Serial(0x42), Map = MapType.Trammel, Location = new Point3D(1496, 1628, 0) });
        await npcs.SpawnAsync("orc", MapType.Trammel, new Point3D(1496, 1628, 0));
        Assert.True(_ticks.IsAwake(_orc.Id));

        Assert.True(await npcs.RemoveAsync(_orc.Id));

        Assert.False(_ticks.IsAwake(_orc.Id));
    }

    [Fact]
    public async Task RemoveAsync_APlayerCharacter_IsRefused()
    {
        var npcs = await CreateAsync();
        var aria = new MobileEntity { Id = new(2), Name = "Aria", AccountId = new Serial(0x42), Map = MapType.Trammel };
        _mobiles.EnterWorld(aria);

        Assert.False(await npcs.RemoveAsync(aria.Id));

        Assert.True(_mobiles.IsInWorld(aria.Id));
        Assert.Empty(_mobiles.Capture());
    }

    [Fact]
    public async Task RemoveAsync_AnUnknownSerial_IsFalse()
    {
        var npcs = await CreateAsync();

        Assert.False(await npcs.RemoveAsync(new Serial(0x00000999)));
    }

    private async Task<NpcService> CreateAsync()
    {
        _fixture = await SessionFixture.CreateAsync();

        return new(
            _factory,
            _mobiles,
            _items,
            _view,
            new RecordingDataAccess<MobileEntity>(),
            new RecordingDataAccess<ItemEntity>(),
            _fixture.Loop,
            _scripts,
            _itemScripts
        );
    }

    public async ValueTask DisposeAsync()
    {
        if (_fixture is not null)
        {
            await _fixture.DisposeAsync();
        }
    }
}
