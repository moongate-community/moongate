using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Network.Packets.Data.Clients;
using Moongate.Scripting.Data.Scripts;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Services.Sessions;
using Moongate.Server.Ultima.Data.Bodies;
using Moongate.Server.Ultima.Data.Config;
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
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Tests.TestSupport.Ultima.Sectors;
using Moongate.Tests.TestSupport.Ultima.Tiles;
using Moongate.Tests.TestSupport.Ultima.Tooltips;
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
    private readonly MobileService _mobiles = new(new StubMovementService(), TestSectors.Create());
    private readonly StubPacketSendService _sender = new StubPacketSendService().Ignore<PropertyListInfoPacket>();
    private readonly FakeTileDataService _tiles = new FakeTileDataService()
                                                  .Item(BackpackGraphic, TileFlagType.Container, 0)
                                                  .Item(BagGraphic, TileFlagType.Container, 0)
                                                  .Item(PouchGraphic, TileFlagType.Container, 0);

    private readonly ItemEntity _backpack = Item(0x40000001, BackpackGraphic);
    private readonly ItemEntity _bag = Item(0x40000002, BagGraphic, "bag");
    private readonly ItemEntity _dagger = Item(0x40000003, DaggerGraphic, "dagger");
    private readonly ItemEntity _coin = Item(0x40000004, 0x0EED);
    private readonly ItemEntity _otherBackpack = Item(0x40000005, BackpackGraphic, "other_backpack");
    private readonly ItemEntity _pouch = Item(0x40000006, PouchGraphic);

    private readonly RecordingItemScriptService _scripts = new();

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
        _mobiles.EnterWorld(Mobile(Aria, "Aria", 401, new(1000, 1000, 0)));
        _mobiles.EnterWorld(Mobile(Bran, "Bran", 400, new(1010, 1000, 0)));
    }

    [Fact]
    public async Task Handle_TheOwnPaperdollRequest_OpensItWithCanLift()
    {
        await StartAsync(Aria);

        await UseAsync(new Serial(Aria.Value | 0x80000000));

        var paperdoll = Assert.IsType<DisplayPaperdollPacket>(Assert.Single(_sender.Sent));
        Assert.Equal((Aria, "Aria", false, true), (paperdoll.Mobile, paperdoll.Title, paperdoll.WarMode, paperdoll.CanLift));
    }

    [Fact]
    public async Task Handle_TheOwnCharacter_OpensItsPaperdoll()
    {
        await StartAsync(Aria);

        await UseAsync(Aria);

        Assert.True(Assert.IsType<DisplayPaperdollPacket>(Assert.Single(_sender.Sent)).CanLift);
    }

    [Fact]
    public async Task Handle_AnotherPlayerInRange_OpensItsPaperdollWithoutCanLift()
    {
        await StartAsync(Aria);

        await UseAsync(Bran);

        var paperdoll = Assert.IsType<DisplayPaperdollPacket>(Assert.Single(_sender.Sent));
        Assert.Equal((Bran, "Bran", false), (paperdoll.Mobile, paperdoll.Title, paperdoll.CanLift));
    }

    [Fact]
    public async Task Handle_AHumanNpcWithATitle_ShowsNameAndTitle()
    {
        var mage = Mobile(new(0x00000010), "Nystul", 400, new(1005, 1000, 0));
        mage.Title = "the mage";
        _mobiles.EnterWorld(mage);
        await StartAsync(Aria);

        await UseAsync(mage.Id);

        Assert.Equal("Nystul, the mage", Assert.IsType<DisplayPaperdollPacket>(Assert.Single(_sender.Sent)).Title);
    }

    [Fact]
    public async Task Handle_AMonster_DoesNotOpenAPaperdoll()
    {
        var orc = Mobile(new(0x00000011), "an orc", 17, new(1005, 1000, 0));
        _mobiles.EnterWorld(orc);
        await StartAsync(Aria);

        await UseAsync(orc.Id);

        Assert.Empty(_sender.Sent);
    }

    [Fact]
    public async Task Handle_ABodyNotInBodiesToml_DoesNotOpenAPaperdoll()
    {
        var ghost = Mobile(new(0x00000014), "a ghost", 970, new(1005, 1000, 0));
        _mobiles.EnterWorld(ghost);
        await StartAsync(Aria);

        await UseAsync(ghost.Id);

        Assert.Empty(_sender.Sent);
    }

    [Fact]
    public async Task Handle_APlayerOutOfViewRange_DoesNotOpenAPaperdoll()
    {
        var far = Mobile(new(0x00000012), "Far", 400, new(1019, 1000, 0));
        _mobiles.EnterWorld(far);
        await StartAsync(Aria);

        await UseAsync(far.Id);

        Assert.Empty(_sender.Sent);
    }

    [Fact]
    public async Task Handle_APlayerOnAnotherMap_DoesNotOpenAPaperdoll()
    {
        var other = Mobile(new(0x00000013), "Other", 400, new(1001, 1000, 0));
        other.Map = MapType.Trammel;
        _mobiles.EnterWorld(other);
        await StartAsync(Aria);

        await UseAsync(other.Id);

        Assert.Empty(_sender.Sent);
    }

    [Fact]
    public async Task Handle_APaperdollRequestWithoutACharacter_IsIgnored()
    {
        await StartAsync(null);

        await UseAsync(new Serial(Aria.Value | 0x80000000));

        Assert.Empty(_sender.Sent);
    }

    [Fact]
    public async Task Handle_ACarriedScriptedItem_RunsOnUseWithTheCharacter()
    {
        _scripts.Scripted.Add("dagger");
        _scripts.Result = ScriptResult.Completed([true]);
        await StartAsync(Aria);

        await UseAsync(_dagger.Id);

        Assert.Equal(["0x40000003 on_use 2"], _scripts.Calls);
        Assert.Empty(_sender.Sent);
    }

    [Fact]
    public async Task Handle_AScriptedBagWhoseScriptReturnsNothing_StillOpens()
    {
        _scripts.Scripted.Add("bag");
        await StartAsync(Aria);

        await UseAsync(_bag.Id);

        Assert.Equal(["0x40000002 on_use 2"], _scripts.Calls);
        Assert.IsType<DisplayContainerPacket>(_sender.Sent[0]);
    }

    [Theory, InlineData(true), InlineData(false)]
    public async Task Handle_AScriptedBagWhoseScriptHandledItOrWaited_DoesNotOpen(bool waited)
    {
        _scripts.Scripted.Add("bag");
        _scripts.Result = waited ? ScriptResult.Suspended : ScriptResult.Completed([true]);
        await StartAsync(Aria);

        await UseAsync(_bag.Id);

        Assert.Single(_scripts.Calls);
        Assert.Empty(_sender.Sent);
    }

    [Fact]
    public async Task Handle_AScriptedItemSomeoneElseCarries_IsTooFarAndRunsNothing()
    {
        _scripts.Scripted.Add("other_backpack");
        await StartAsync(Aria);

        await UseAsync(_otherBackpack.Id);

        Assert.Empty(_scripts.Calls);
        var message = Assert.IsType<LocalizedMessagePacket>(Assert.Single(_sender.Sent));
        Assert.Equal(500446, message.Cliloc);
    }

    [Theory, InlineData(1002, true), InlineData(1003, false)]
    public async Task Handle_AScriptedGroundItem_RunsOnlyWithinTwoTiles(int x, bool reached)
    {
        var torch = new ItemEntity { Id = new(0x40000010), TemplateId = "torch", ItemId = 0x0F64, Amount = 1 };
        torch.PlaceOnGround(MapType.Felucca, new Point3D(x, 1000, 0));
        _items.Add([torch]);
        _scripts.Scripted.Add("torch");
        await StartAsync(Aria);

        await UseAsync(torch.Id);

        Assert.Equal(reached ? ["0x40000010 on_use 2"] : [], _scripts.Calls);
        Assert.Equal(!reached, _sender.Sent.OfType<LocalizedMessagePacket>().Any(message => message.Cliloc == 500446));
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
        // As ModernUO, each item shown is followed by its tooltip revision.
        Assert.Equal([_bag.Id, _dagger.Id, _pouch.Id], _sender.Ignored.Cast<PropertyListInfoPacket>().Select(info => info.Serial));
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
        var bodies = new StubDataLoaderService().With(
            new BodyContent { Body = new(400), Type = BodyType.Human },
            new BodyContent { Body = new(401), Type = BodyType.Human },
            new BodyContent { Body = new(17), Type = BodyType.Monster }
        );
        var handler = new UseRequestPacketHandler(_items, _mobiles, bodies, new WorldConfig(), _tiles, layouts, _sender, TestTooltips.Create(_items, _mobiles), _scripts);

        return _fixture.ExecuteOnLoopAsync(() => handler.Handle(_session, new UseRequestPacket { Target = target }));
    }

    private static MobileEntity Mobile(Serial id, string name, int body, Point3D location)
    {
        return new() { Id = id, Name = name, Body = body, Map = MapType.Felucca, Location = location };
    }

    private static ItemEntity Item(uint serial, int graphic, string template = "item")
    {
        return new() { Id = new(serial), TemplateId = template, ItemId = graphic, Amount = 1 };
    }

    public async ValueTask DisposeAsync()
    {
        if (_fixture is not null)
        {
            await _fixture.DisposeAsync();
        }
    }
}
