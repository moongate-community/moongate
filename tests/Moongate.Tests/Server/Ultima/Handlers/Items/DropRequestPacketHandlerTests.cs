using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Services.Sessions;
using Moongate.Server.Ultima.Data.Containers;
using Moongate.Server.Ultima.Data.Items;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Handlers.Items;
using Moongate.Server.Ultima.Packets.General;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.Support.Sessions;
using Moongate.Tests.TestSupport.Packets;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Tiles;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Handlers.Items;

public sealed class DropRequestPacketHandlerTests : IAsyncDisposable
{
    private const int BackpackGraphic = 0x0E75;
    private const int BagGraphic = 0x0E76;
    private const int CoinGraphic = 0x0EED;

    private static readonly Serial Aria = new(0x00000002);
    private static readonly Serial Bran = new(0x00000003);
    private static readonly Serial Ground = new(0xFFFFFFFF);

    private readonly ItemService _items = new();
    private readonly StubPacketSendService _sender = new();
    private readonly FakeTileDataService _tiles = new FakeTileDataService()
                                                  .Item(BackpackGraphic, TileFlagType.Container, 0)
                                                  .Item(BagGraphic, TileFlagType.Container, 0);

    private readonly ItemEntity _backpack = Item(0x40000001, BackpackGraphic);
    private readonly ItemEntity _bag = Item(0x40000002, BagGraphic);
    private readonly ItemEntity _innerBag = Item(0x40000003, BagGraphic);
    private readonly ItemEntity _coins = Item(0x40000004, CoinGraphic);
    private readonly ItemEntity _dagger = Item(0x40000005, 0x0F52);
    private readonly ItemEntity _otherBackpack = Item(0x40000006, BackpackGraphic);

    private SessionFixture _fixture = null!;
    private GameSession _session = null!;

    public DropRequestPacketHandlerTests()
    {
        _backpack.Equip(Aria, LayerType.Backpack);
        _bag.PutInContainer(_backpack.Id, new Point2D(50, 50));
        _innerBag.PutInContainer(_bag.Id, new Point2D(30, 30));
        _coins.PutInContainer(_backpack.Id, new Point2D(44, 65));
        _dagger.PutInContainer(_backpack.Id, new Point2D(100, 90));
        _otherBackpack.Equip(Bran, LayerType.Backpack);
        _items.Add([_backpack, _bag, _innerBag, _coins, _dagger, _otherBackpack]);
    }

    [Fact]
    public async Task Handle_IntoTheBackpackAtAPosition_MovesTheItemAndShowsItThere()
    {
        await HoldingAsync(_coins);

        await DropAsync(_coins.Id, 80, 70, _backpack.Id);

        AssertAt(_coins, _backpack.Id, new Point2D(80, 70));
    }

    [Fact]
    public async Task Handle_OutsideTheGumpBounds_IsBroughtInside()
    {
        await HoldingAsync(_coins);

        await DropAsync(_coins.Id, 500, -20, _backpack.Id);

        AssertAt(_coins, _backpack.Id, new Point2D(139, 60));
    }

    [Fact]
    public async Task Handle_OnTheContainerIcon_TakesARandomSpotInsideTheBounds()
    {
        await HoldingAsync(_coins);

        await DropAsync(_coins.Id, -1, -1, _backpack.Id);

        Assert.Equal(_backpack.Id, _coins.ContainerId);
        Assert.InRange(_coins.GridX!.Value, (short)44, (short)139);
        Assert.InRange(_coins.GridY!.Value, (short)60, (short)129);
        AssertShownWhereItIs(_coins);
    }

    [Fact]
    public async Task Handle_IntoABag_MovesTheItemIntoIt()
    {
        await HoldingAsync(_coins);

        await DropAsync(_coins.Id, 60, 70, _bag.Id);

        AssertAt(_coins, _bag.Id, new Point2D(60, 70));
    }

    [Fact]
    public async Task Handle_OntoAnItemThatIsNotAContainer_GoesIntoItsContainerAtItsPosition()
    {
        await HoldingAsync(_coins);

        await DropAsync(_coins.Id, 0, 0, _dagger.Id);

        AssertAt(_coins, _backpack.Id, new Point2D(100, 90));
    }

    [Fact]
    public async Task Handle_ABagIntoItself_Bounces()
    {
        await HoldingAsync(_bag);

        await DropAsync(_bag.Id, 60, 70, _bag.Id);

        AssertAt(_bag, _backpack.Id, new Point2D(50, 50));
    }

    [Fact]
    public async Task Handle_ABagIntoABagInsideIt_Bounces()
    {
        await HoldingAsync(_bag);

        await DropAsync(_bag.Id, 60, 70, _innerBag.Id);

        AssertAt(_bag, _backpack.Id, new Point2D(50, 50));
    }

    [Fact]
    public async Task Handle_OnTheGround_Bounces()
    {
        await HoldingAsync(_coins);

        await DropAsync(_coins.Id, 1496, 1628, Ground);

        AssertAt(_coins, _backpack.Id, new Point2D(44, 65));
    }

    [Fact]
    public async Task Handle_IntoAnotherCharactersContainer_Bounces()
    {
        await HoldingAsync(_coins);

        await DropAsync(_coins.Id, 60, 70, _otherBackpack.Id);

        AssertAt(_coins, _backpack.Id, new Point2D(44, 65));
    }

    [Fact]
    public async Task Handle_OnAMobile_Bounces()
    {
        await HoldingAsync(_coins);

        await DropAsync(_coins.Id, 0, 0, Aria);

        AssertAt(_coins, _backpack.Id, new Point2D(44, 65));
    }

    [Fact]
    public async Task Handle_NothingHeld_SendsNothing()
    {
        await StartAsync();

        await DropAsync(_coins.Id, 60, 70, _backpack.Id);

        Assert.Empty(_sender.Sent);
        Assert.Equal(new Point2D(44, 65), _coins.GridLocation);
    }

    [Fact]
    public async Task Handle_AnotherItemThanTheHeldOne_MovesNothingAndFreesTheHand()
    {
        await HoldingAsync(_coins);

        await DropAsync(_dagger.Id, 60, 70, _bag.Id);

        Assert.Empty(_sender.Sent);
        Assert.Equal((_backpack.Id, _backpack.Id), (_coins.ContainerId!.Value, _dagger.ContainerId!.Value));
        Assert.Null(_session.Get(ItemSessionKeys.Held));
    }

    [Fact]
    public async Task Handle_EveryDrop_FreesTheHand()
    {
        await HoldingAsync(_coins);

        await DropAsync(_coins.Id, 1496, 1628, Ground);

        Assert.Null(_session.Get(ItemSessionKeys.Held));
    }

    private void AssertAt(ItemEntity item, Serial container, Point2D position)
    {
        Assert.Equal((container, position), (item.ContainerId!.Value, item.GridLocation!.Value));
        AssertShownWhereItIs(item);
        Assert.Null(_session.Get(ItemSessionKeys.Held));
    }

    private void AssertShownWhereItIs(ItemEntity item)
    {
        var update = Assert.IsType<ContainerItemUpdatePacket>(Assert.Single(_sender.Sent));
        Assert.Equal(
            (item.Id, item.ContainerId!.Value, (int)item.GridX!.Value, (int)item.GridY!.Value),
            (update.Item.Serial, update.Item.Container, update.Item.GridX, update.Item.GridY)
        );
    }

    private async Task StartAsync()
    {
        _fixture = await SessionFixture.CreateAsync();
        _session = new SessionService(_fixture.Loop).GetOrCreate(_fixture.Client);
        await _fixture.ExecuteOnLoopAsync(() => _session.Set(SessionKeys.CharacterId, Aria));
    }

    private async Task HoldingAsync(ItemEntity item)
    {
        await StartAsync();
        await _fixture.ExecuteOnLoopAsync(() => _session.Set(ItemSessionKeys.Held, new(item.Id)));
    }

    private Task DropAsync(Serial item, short x, short y, Serial destination)
    {
        var layouts = new ContainerLayoutService(
            new StubDataLoaderService().With(
                new ContainerContent
                {
                    Name = "backpack", Gump = 0x003C, Items = [BackpackGraphic, BagGraphic], Default = true,
                    Bounds = new Rectangle2D(new Point2D(44, 60), new Point2D(140, 130))
                }
            )
        );
        var handler = new DropRequestPacketHandler(_items, _tiles, layouts, _sender);
        var packet = new DropRequestPacket { Item = item, X = x, Y = y, Z = 0, Destination = destination };

        return _fixture.ExecuteOnLoopAsync(() => handler.Handle(_session, packet));
    }

    private static ItemEntity Item(uint serial, int graphic)
    {
        return new() { Id = new(serial), TemplateId = "item", ItemId = graphic, Amount = 1 };
    }

    public async ValueTask DisposeAsync()
    {
        if (_fixture is not null)
        {
            await _fixture.DisposeAsync();
        }
    }
}
