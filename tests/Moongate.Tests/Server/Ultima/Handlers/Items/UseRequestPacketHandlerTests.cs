using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Network.Packets.Data.Clients;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Services.Sessions;
using Moongate.Server.Ultima.Data.Containers;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Handlers.Items;
using Moongate.Server.Ultima.Packets.General;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.Support.Sessions;
using Moongate.Tests.TestSupport.Packets;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Tiles;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Handlers.Items;

public sealed class UseRequestPacketHandlerTests : IAsyncDisposable
{
    private const int BackpackGraphic = 0x0E75;
    private const int BagGraphic = 0x0E76;
    private const int PouchGraphic = 0x0E79;
    private const int DaggerGraphic = 0x0F52;

    private static readonly Serial Aria = new(0x00000002);
    private static readonly Serial Bran = new(0x00000003);

    private readonly ItemService _items = TestItems.Create();
    private readonly StubPacketSendService _sender = new();
    private readonly FakeTileDataService _tiles = new FakeTileDataService()
                                                  .Item(BackpackGraphic, TileFlagType.Container, 0)
                                                  .Item(BagGraphic, TileFlagType.Container, 0)
                                                  .Item(PouchGraphic, TileFlagType.Container, 0);

    private readonly ItemEntity _backpack = Item(0x40000001, BackpackGraphic);
    private readonly ItemEntity _bag = Item(0x40000002, BagGraphic);
    private readonly ItemEntity _dagger = Item(0x40000003, DaggerGraphic);
    private readonly ItemEntity _coin = Item(0x40000004, 0x0EED);
    private readonly ItemEntity _otherBackpack = Item(0x40000005, BackpackGraphic);
    private readonly ItemEntity _pouch = Item(0x40000006, PouchGraphic);

    private SessionFixture _fixture = null!;
    private GameSession _session = null!;

    public UseRequestPacketHandlerTests()
    {
        _backpack.Equip(Aria, LayerType.Backpack);
        _bag.PutInContainer(_backpack.Id, new Point2D(44, 65));
        _dagger.PutInContainer(_backpack.Id, new Point2D(60, 80));
        _coin.PutInContainer(_bag.Id, new Point2D(30, 30));
        _otherBackpack.Equip(Bran, LayerType.Backpack);
        _pouch.PutInContainer(_backpack.Id, new Point2D(90, 90));
        _items.Add([_backpack, _bag, _dagger, _coin, _otherBackpack, _pouch]);
    }

    [Fact]
    public async Task Handle_TheOwnBackpack_OpensItsGumpThenListsItsItems()
    {
        await StartAsync(Aria);

        await UseAsync(_backpack.Id);

        Assert.Equal([typeof(DisplayContainerPacket), typeof(ContainerContentPacket)], _sender.Sent.Select(packet => packet.GetType()));
        var display = (DisplayContainerPacket)_sender.Sent[0];
        Assert.Equal((_backpack.Id, 0x003C, true), (display.Container, display.Gump, display.HighSeas));
        var content = (ContainerContentPacket)_sender.Sent[1];
        Assert.Equal([_bag.Id, _dagger.Id, _pouch.Id], content.Items.Select(item => item.Serial));
        Assert.True(content.GridBytes);
    }

    [Fact]
    public async Task Handle_ABagInsideTheBackpack_Opens()
    {
        await StartAsync(Aria);

        await UseAsync(_bag.Id);

        var display = Assert.IsType<DisplayContainerPacket>(_sender.Sent[0]);
        Assert.Equal((_bag.Id, 0x003D), (display.Container, display.Gump));
        Assert.Equal([_coin.Id], ((ContainerContentPacket)_sender.Sent[1]).Items.Select(item => item.Serial));
    }

    [Fact]
    public async Task Handle_AnEmptyContainer_StillListsZeroItems()
    {
        await StartAsync(Aria);
        _items.Remove([_coin.Id]);

        await UseAsync(_bag.Id);

        Assert.Empty(Assert.IsType<ContainerContentPacket>(_sender.Sent[1]).Items);
    }

    [Fact]
    public async Task Handle_AContainerWithoutItsOwnLayout_UsesTheDefaultGump()
    {
        await StartAsync(Aria);

        await UseAsync(_pouch.Id);

        Assert.Equal(0x003C, Assert.IsType<DisplayContainerPacket>(_sender.Sent[0]).Gump);
    }

    [Fact]
    public async Task Handle_AnItemThatIsNotAContainer_SendsNothing()
    {
        await StartAsync(Aria);

        await UseAsync(_dagger.Id);

        Assert.Empty(_sender.Sent);
    }

    [Fact]
    public async Task Handle_AGraphicInTheLayoutsWithoutTheContainerFlag_SendsNothing()
    {
        await StartAsync(Aria);
        _tiles.Item(BagGraphic, TileFlagType.None, 0);

        await UseAsync(_bag.Id);

        Assert.Empty(_sender.Sent);
    }

    [Fact]
    public async Task Handle_AnotherCharactersBackpack_SendsNothing()
    {
        await StartAsync(Aria);

        await UseAsync(_otherBackpack.Id);

        Assert.Empty(_sender.Sent);
    }

    [Fact]
    public async Task Handle_WithoutACharacter_SendsNothing()
    {
        await StartAsync(null);

        await UseAsync(_backpack.Id);

        Assert.Empty(_sender.Sent);
    }

    [Fact]
    public async Task Handle_AnOldClient_GetsTheShortFormats()
    {
        await StartAsync(Aria);
        _session.NetworkSession.SetClientVersion(new ClientVersion(6, 0, 1, 6));

        await UseAsync(_backpack.Id);

        Assert.False(((DisplayContainerPacket)_sender.Sent[0]).HighSeas);
        Assert.False(((ContainerContentPacket)_sender.Sent[1]).GridBytes);
    }

    [Fact]
    public async Task Handle_AClientBeforeHighSeas_GetsTheGridBytesButTheShortGump()
    {
        await StartAsync(Aria);
        _session.NetworkSession.SetClientVersion(new ClientVersion(7, 0, 8, 0));

        await UseAsync(_backpack.Id);

        Assert.False(((DisplayContainerPacket)_sender.Sent[0]).HighSeas);
        Assert.True(((ContainerContentPacket)_sender.Sent[1]).GridBytes);
    }

    private async Task StartAsync(Serial? character)
    {
        _fixture = await SessionFixture.CreateAsync();
        _session = new SessionService(_fixture.Loop).GetOrCreate(_fixture.Client);

        if (character is { } id)
        {
            await _fixture.ExecuteOnLoopAsync(() => _session.Set(SessionKeys.CharacterId, id));
        }
    }

    private Task UseAsync(Serial target)
    {
        var layouts = new ContainerLayoutService(
            new StubDataLoaderService().With(
                new ContainerContent { Name = "backpack", Gump = 0x003C, Items = [BackpackGraphic], Default = true },
                new ContainerContent { Name = "bag", Gump = 0x003D, Items = [BagGraphic] }
            )
        );
        var handler = new UseRequestPacketHandler(_items, _tiles, layouts, _sender);

        return _fixture.ExecuteOnLoopAsync(() => handler.Handle(_session, new UseRequestPacket { Target = target }));
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
