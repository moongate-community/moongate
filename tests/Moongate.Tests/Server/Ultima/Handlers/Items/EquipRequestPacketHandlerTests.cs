using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Services.Sessions;
using Moongate.Server.Ultima.Data.Items;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Handlers.Items;
using Moongate.Server.Ultima.Packets.General;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.Support.Sessions;
using Moongate.Tests.TestSupport.Packets;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Tests.TestSupport.Ultima.Sectors;
using Moongate.Tests.TestSupport.Ultima.Tiles;
using Moongate.Tests.TestSupport.Ultima.World;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Handlers.Items;

public sealed class EquipRequestPacketHandlerTests : IAsyncDisposable
{
    private static readonly Serial Aria = new(0x00000002);
    private static readonly Serial Bran = new(0x00000003);

    private readonly ItemService _items;
    private readonly MobileService _mobiles;
    private readonly RecordingWorldViewService _view = new();
    private readonly StubPacketSendService _sender = new();
    private readonly EquipmentService _equipment;
    private readonly ItemEntity _backpack = Item(0x40000001, "backpack", 0x0E75);
    private readonly ItemEntity _dagger = Item(0x40000002, "dagger", 0x0F52);
    private readonly ItemEntity _apple = Item(0x40000003, "apple", 0x09D0);
    private readonly ItemEntity _shirt = Item(0x40000004, "shirt", 0x1517);
    private readonly ItemEntity _otherShirt = Item(0x40000005, "shirt", 0x1517);
    private readonly ItemEntity _groundDagger = Item(0x40000006, "dagger", 0x0F52);

    private SessionFixture _fixture = null!;
    private GameSession _session = null!;

    public EquipRequestPacketHandlerTests()
    {
        var sectors = TestSectors.Create();
        _items = TestItems.Create(sectors);
        _mobiles = new(new StubMovementService(), sectors);
        _mobiles.EnterWorld(new() { Id = Aria, Name = "Aria", Map = MapType.Trammel, Location = new Point3D(1496, 1628, 0) });
        _mobiles.EnterWorld(new() { Id = Bran, Name = "Bran", Map = MapType.Trammel, Location = new Point3D(1497, 1628, 0) });
        _backpack.Equip(Aria, LayerType.Backpack);
        _dagger.PutInContainer(_backpack.Id, new Point2D(60, 80));
        _apple.PutInContainer(_backpack.Id, new Point2D(70, 80));
        _shirt.PutInContainer(_backpack.Id, new Point2D(80, 80));
        _otherShirt.Equip(Aria, LayerType.Shirt);
        _items.Add([_backpack, _dagger, _apple, _shirt, _otherShirt, _groundDagger]);
        _items.PlaceOnGround(_groundDagger, MapType.Trammel, new Point3D(1497, 1628, 0));
        _equipment = new(
            new ItemTemplateService(
                new StubDataLoaderService().With(
                    Template("backpack", 0x0E75, LayerType.Backpack),
                    Template("dagger", 0x0F52, LayerType.OneHanded),
                    Template("shirt", 0x1517, LayerType.Shirt),
                    Template("apple", 0x09D0)
                )
            ),
            new FakeTileDataService().Item(0x09D0, TileFlagType.None, 0),
            _items
        );
    }

    [Fact]
    public async Task Handle_AWearableItemOnTheOwnPaperdoll_PutsItOnAndShowsItToEveryone()
    {
        await StartAsync(_dagger);

        await EquipAsync(_dagger, Aria);

        Assert.Null(_session.Get(ItemSessionKeys.Held));
        Assert.Equal((Aria, LayerType.OneHanded), (_dagger.MobileId!.Value, _dagger.Layer!.Value));
        Assert.Contains(_dagger, _items.GetWorn(Aria));
        Assert.Equal([$"Worn {Aria.Value} {_dagger.Id.Value}"], _view.Calls);
        Assert.Empty(_sender.Sent);
    }

    [Fact]
    public async Task Handle_TheClientsLayer_IsIgnoredForTheItemsOwn()
    {
        await StartAsync(_dagger);

        await EquipAsync(_dagger, Aria, LayerType.Helm);

        Assert.Equal(LayerType.OneHanded, _dagger.Layer);
    }

    [Fact]
    public async Task Handle_AGroundItemHeld_IsTakenOffTheGroundAndWorn()
    {
        await StartAsync(_groundDagger);
        _items.Hide(_groundDagger);

        await EquipAsync(_groundDagger, Aria);

        Assert.Null(_groundDagger.GroundLocation);
        Assert.Equal(Aria, _groundDagger.MobileId);
        Assert.False(_items.IsLyingOnGround(_groundDagger));
    }

    [Fact]
    public async Task Handle_AnotherCharactersPaperdoll_BouncesTheItemBack()
    {
        await StartAsync(_dagger);

        await EquipAsync(_dagger, Bran);

        AssertBouncedToTheBackpack(_dagger);
    }

    [Fact]
    public async Task Handle_ATakenLayer_BouncesTheItemBack()
    {
        await StartAsync(_shirt);

        await EquipAsync(_shirt, Aria);

        AssertBouncedToTheBackpack(_shirt);
    }

    [Fact]
    public async Task Handle_AStackOfMoreThanOne_BouncesBack()
    {
        _dagger.Amount = 3;
        await StartAsync(_dagger);

        await EquipAsync(_dagger, Aria);

        AssertBouncedToTheBackpack(_dagger);
    }

    [Fact]
    public async Task Handle_AnItemThatIsNotWorn_BouncesBack()
    {
        await StartAsync(_apple);

        await EquipAsync(_apple, Aria);

        AssertBouncedToTheBackpack(_apple);
    }

    [Fact]
    public async Task Handle_AWornItemThatCannotGoOn_GoesBackOnTheWearer()
    {
        // Picked up from the paperdoll: still worn, so the bounce puts it back on and shows it again.
        await StartAsync(_otherShirt);

        await EquipAsync(_otherShirt, Bran);

        Assert.Equal((Aria, LayerType.Shirt), (_otherShirt.MobileId!.Value, _otherShirt.Layer!.Value));
        Assert.Equal([$"Worn {Aria.Value} {_otherShirt.Id.Value}"], _view.Calls);
    }

    [Fact]
    public async Task Handle_AGroundItemThatCannotGoOn_LiesThereAgain()
    {
        await StartAsync(_groundDagger);
        _items.Hide(_groundDagger);

        await EquipAsync(_groundDagger, Bran);

        Assert.True(_items.IsLyingOnGround(_groundDagger));
        Assert.Equal([$"Appeared {_groundDagger.Id.Value}"], _view.Calls);
    }

    [Fact]
    public async Task Handle_ASerialThatIsNotTheHeldItem_BouncesTheHeldItem()
    {
        await StartAsync(_dagger);

        await EquipAsync(_apple, Aria);

        AssertBouncedToTheBackpack(_dagger);
    }

    [Fact]
    public async Task Handle_NothingHeld_SendsNothing()
    {
        await StartAsync(null);

        await EquipAsync(_dagger, Aria);

        Assert.Empty(_sender.Sent);
        Assert.Empty(_view.Calls);
        Assert.Equal(_backpack.Id, _dagger.ContainerId);
    }

    private void AssertBouncedToTheBackpack(ItemEntity item)
    {
        Assert.Null(_session.Get(ItemSessionKeys.Held));
        Assert.Equal(_backpack.Id, item.ContainerId);
        Assert.Equal(item.Id, Assert.IsType<ContainerItemUpdatePacket>(Assert.Single(_sender.Sent)).Item.Serial);
        Assert.Empty(_view.Calls);
    }

    private async Task StartAsync(ItemEntity? held)
    {
        _fixture = await SessionFixture.CreateAsync();
        _session = new SessionService(_fixture.Loop).GetOrCreate(_fixture.Client);
        await _fixture.ExecuteOnLoopAsync(() =>
            {
                _session.Set(SessionKeys.CharacterId, Aria);

                if (held is not null)
                {
                    _session.Set(ItemSessionKeys.Held, new(held.Id));
                }
            }
        );
    }

    private Task EquipAsync(ItemEntity item, Serial mobile, LayerType layer = LayerType.OneHanded)
    {
        var handler = new EquipRequestPacketHandler(_items, _mobiles, _equipment, _view, _sender);

        return _fixture.ExecuteOnLoopAsync(() =>
            handler.Handle(_session, new EquipRequestPacket { Item = item.Id, Layer = layer, Mobile = mobile })
        );
    }

    private static ItemEntity Item(uint serial, string template, int graphic)
    {
        return new() { Id = new(serial), TemplateId = template, ItemId = graphic, Amount = 1 };
    }

    private static ItemTemplate Template(string id, int itemId, LayerType? layer = null)
    {
        return new() { Id = id, ItemId = new Serial((uint)itemId), Layer = layer };
    }

    public async ValueTask DisposeAsync()
    {
        if (_fixture is not null)
        {
            await _fixture.DisposeAsync();
        }
    }
}
