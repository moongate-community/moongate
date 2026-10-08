using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Network.Packets.Serialization;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.Containers;
using Moongate.Server.Ultima.Data.Regions;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Data.Templates.Mobiles;
using Moongate.Server.Ultima.Data.Templates.Shops;
using Moongate.Server.Ultima.Data.Vendors;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Packets.Vendors;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Types.Bank;
using Moongate.Tests.TestSupport.Ultima.Bank;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Tests.TestSupport.Ultima.Tiles;
using Moongate.Tests.TestSupport.Ultima.Tooltips;
using Moongate.Tests.TestSupport.Ultima.Weight;
using Moongate.Tests.TestSupport.Ultima.World;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class VendorServiceTests : IAsyncLifetime
{
    private readonly ItemService _items = TestItems.Create();
    private readonly StubItemSerialPool _serials = new();
    private readonly StubBankService _bank = new();
    private readonly RecordingSpeechService _speech = new();
    private readonly StubLineOfSightService _sight = new();
    private readonly RecordingWorldViewService _view = new();
    private readonly StubWeightService _weight = new();
    private readonly RegionContent _town = new()
    {
        Name = "Town", Map = MapType.Trammel, Guarded = true, Areas = [new() { X1 = 0, Y1 = 0, X2 = 500, Y2 = 500 }]
    };

    private BroadcastFixture _fixture = null!;
    private GameSession _session = null!;
    private MobileEntity _player = null!;
    private MobileEntity _vendor = null!;
    private VendorService _vendors = null!;
    private RegionService _regions = null!;
    private uint _nextItem = 0x40001000;

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        _session = await _fixture.AddAsync(2);
        Assert.True(_fixture.Mobiles.TryGet(new Serial(2), out var player));
        _player = player;
        _player.AccountId = new Serial(1002);
        _player.Location = new Point3D(12, 10, 0);
        _vendor = new MobileEntity
        {
            Id = new Serial(100), Name = "a baker", TemplateId = "baker", Map = MapType.Trammel,
            Location = new Point3D(10, 10, 0)
        };
        await _fixture.Network.ExecuteOnLoopAsync(() => _fixture.Mobiles.EnterWorld(_vendor));

        var itemTemplates = new ItemTemplateService(
            new StubDataLoaderService().With(
                new ItemTemplate { Id = "bread", ItemId = new Serial(0x103B) },
                new ItemTemplate { Id = "sword", ItemId = new Serial(0x0F5E) },
                new ItemTemplate { Id = "backpack", ItemId = new Serial(0x0E75), MaxWeight = 400 }
            )
        );
        var tiles = new FakeTileDataService().Item(0x103B, TileFlagType.Generic, 0);
        var shops = new ShopService(
            new StubDataLoaderService().With(
                new ShopDefinition
                {
                    Id = "baker", Vendors = ["baker"],
                    Buy =
                    [
                        new() { Item = "bread", Price = 8, Amount = 20 },
                        new() { Item = "sword", Price = 1000, Amount = 5, Name = "Fine sword" }
                    ]
                }
            )
        );
        _regions = new(new StubDataLoaderService().With(_town));
        var bankConfig = new BankConfig();

        for (uint index = 0; index < 40; index++)
        {
            _serials.Serials.Enqueue(new Serial(0x40002000 + index));
        }

        _vendors = new(
            shops,
            itemTemplates,
            tiles,
            _fixture.Mobiles,
            _items,
            new ItemHandlingService(
                _items,
                _fixture.Sessions,
                _fixture.Sender,
                _view,
                TestTooltips.Create(_items, _fixture.Mobiles),
                new FakeItemFactoryService(itemTemplates, tiles),
                _serials
            ),
            _serials,
            _bank,
            _fixture.Sender,
            _speech,
            _sight,
            _regions,
            new ContainerCapacityService(_items, itemTemplates, bankConfig),
            new ContainerLayoutService(
                new StubDataLoaderService().With(
                    new ContainerContent { Name = "backpack", Gump = 0x003C, Items = [0x0E75], Default = true }
                )
            ),
            _weight,
            _view
        );
    }

    [Fact]
    public async Task OpenBuy_SendsTheContainersTheLinesTheNamesAndTheGump_InThatOrder()
    {
        Assert.True(await OnLoopAsync(() => _vendors.OpenBuy(_session, _vendor)));

        var sent = _fixture.Sender.Sent;
        Assert.Equal(
            [
                typeof(WornItemPacket), typeof(WornItemPacket), typeof(ContainerContentPacket),
                typeof(VendorBuyListPacket), typeof(DisplayContainerPacket)
            ],
            sent.Select(packet => packet.GetType())
        );
        var worn = sent.OfType<WornItemPacket>().ToList();
        Assert.Equal([LayerType.ShopBuy, LayerType.ShopResale], worn.Select(packet => packet.Layer));
        Assert.All(worn, packet => Assert.True(packet.Item.IsVirtual && packet.Wearer == _vendor.Id));

        // The lines are written from the last to the first.
        var lines = sent.OfType<ContainerContentPacket>().Single().Items;
        Assert.Equal([0x0F5E, 0x103B], lines.Select(line => line.ItemId));
        Assert.Equal([5, 20], lines.Select(line => line.Amount));
        Assert.All(lines, line => Assert.Equal(worn[0].Item, line.Container));
        Assert.Equal(
            PacketCodec.Encode(
                new VendorBuyListPacket(
                    worn[0].Item,
                    [new VendorBuyListEntry(8, "1024155"), new VendorBuyListEntry(1000, "Fine sword")]
                )
            ),
            PacketCodec.Encode(sent.OfType<VendorBuyListPacket>().Single())
        );
        Assert.Equal((_vendor.Id, 0x30), (sent.OfType<DisplayContainerPacket>().Single().Container, 0x30));
    }

    [Fact]
    public async Task OpenBuy_AMobileWithNoShop_OrOutOfTheWorld_OpensNothing()
    {
        var stranger = new MobileEntity { Id = new Serial(101), TemplateId = "orc", Map = MapType.Trammel };
        await _fixture.Network.ExecuteOnLoopAsync(() => _fixture.Mobiles.EnterWorld(stranger));
        var gone = new MobileEntity { Id = new Serial(102), TemplateId = "baker", Map = MapType.Trammel };

        Assert.False(await OnLoopAsync(() => _vendors.OpenBuy(_session, stranger)));
        Assert.False(await OnLoopAsync(() => _vendors.OpenBuy(_session, gone)));
        Assert.Empty(_fixture.Sender.Sent);
    }

    [Theory,
     InlineData(11, 10, true),
     InlineData(10, 20, true),
     InlineData(20, 10, true),
     InlineData(21, 10, false),
     InlineData(10, 21, false)]
    public async Task OpenBuy_OnlyWithinTenTiles(int x, int y, bool opens)
    {
        _player.Location = new Point3D(x, y, 0);

        Assert.Equal(opens, await OnLoopAsync(() => _vendors.OpenBuy(_session, _vendor)));
    }

    [Fact]
    public async Task OpenBuy_OutOfSight_OrDead_OpensNothing()
    {
        _sight.Allow = false;
        Assert.False(await OnLoopAsync(() => _vendors.OpenBuy(_session, _vendor)));

        _sight.Allow = true;
        _player.Body = 0x192;
        Assert.False(await OnLoopAsync(() => _vendors.OpenBuy(_session, _vendor)));
        Assert.Empty(_fixture.Sender.Sent);
    }

    [Fact]
    public async Task OpenBuy_AMurdererInAGuardedPlace_IsRefusedByTheVendorsVoice()
    {
        _player.Kills = MobileEntity.MurderKills;
        _vendor.Location = new Point3D(10, 10, 0);

        Assert.False(await OnLoopAsync(() => _vendors.OpenBuy(_session, _vendor)));

        Assert.Equal(501522, Assert.Single(_speech.SaidClilocs).Cliloc);
        Assert.Empty(_fixture.Sender.Sent);
    }

    [Fact]
    public async Task Buy_PaidFromTheBackpack_GivesTheGoods_TakesTheGold_AndEndsTheWindow()
    {
        Backpack();
        _bank.Carried[_player.Id] = 100;
        var lines = await OpenAsync();

        await BuyAsync(Reply(_vendor.Id, (lines[0], 2)));

        var bread = Assert.Single(_items.GetContents(BackpackId()));
        Assert.Equal(("bread", 2), (bread.TemplateId, bread.Amount));
        Assert.Equal(84, _bank.Carried[_player.Id]);
        Assert.Equal(16, Assert.Single(_bank.Taken).Amount);
        Assert.Equal(_vendor.Id, Assert.Single(_fixture.Sender.Sent.OfType<VendorEndPacket>()).Vendor);
        Assert.Equal((1151639, "16"), (_speech.ToldClilocs.Single().Cliloc, _speech.ToldClilocs.Single().Arguments));
        Assert.Null(_session.Get(VendorSessionKeys.Window));
    }

    [Fact]
    public async Task Buy_TheStockDrops_AndTheNextWindowShowsWhatIsLeft()
    {
        Backpack();
        _bank.Carried[_player.Id] = 1000;
        var lines = await OpenAsync();
        await BuyAsync(Reply(_vendor.Id, (lines[0], 5)));
        _fixture.Sender.Sent.Clear();

        await OnLoopAsync(() => _vendors.OpenBuy(_session, _vendor));

        Assert.Equal(15, _fixture.Sender.Sent.OfType<ContainerContentPacket>().Single().Items.Last().Amount);
    }

    [Fact]
    public async Task Buy_ALineSentTwice_AddsUp()
    {
        Backpack();
        _bank.Carried[_player.Id] = 100;
        var lines = await OpenAsync();

        await BuyAsync(Reply(_vendor.Id, (lines[0], 1), (lines[0], 2)));

        Assert.Equal(3, Assert.Single(_items.GetContents(BackpackId())).Amount);
        Assert.Equal(24, Assert.Single(_bank.Taken).Amount);
    }

    [Fact]
    public async Task Buy_ANonStackableLine_GivesOneItemForEachPiece()
    {
        Backpack();
        _bank.Carried[_player.Id] = 3000;
        var lines = await OpenAsync();

        await BuyAsync(Reply(_vendor.Id, (lines[1], 3)));

        Assert.Equal(["sword", "sword", "sword"], _items.GetContents(BackpackId()).Select(item => item.TemplateId));
        Assert.All(_items.GetContents(BackpackId()), item => Assert.Equal(1, item.Amount));
    }

    [Fact]
    public async Task Buy_ASmallPriceThePackCannotPay_IsNotPaidFromTheBank()
    {
        Backpack();
        _bank.Gold[_player.Id] = 100000;
        _bank.Carried[_player.Id] = 0;
        var lines = await OpenAsync();

        await BuyAsync(Reply(_vendor.Id, (lines[0], 20)));

        Assert.Empty(_bank.Withdrawn);
        Assert.Equal(500192, _speech.ToldClilocs.Single().Cliloc);
        Assert.Empty(_items.GetContents(BackpackId()));
    }

    [Fact]
    public async Task Buy_From2000_TheBankMakesUpWhatThePackLacks()
    {
        Backpack();
        _bank.Gold[_player.Id] = 5000;
        _bank.Carried[_player.Id] = 500;
        var lines = await OpenAsync();

        await BuyAsync(Reply(_vendor.Id, (lines[1], 2)));

        Assert.Equal(1500, Assert.Single(_bank.Withdrawn).Amount);
        Assert.Equal(2000, Assert.Single(_bank.Taken).Amount);
        Assert.Equal((1151638, "2000"), (_speech.ToldClilocs.Single().Cliloc, _speech.ToldClilocs.Single().Arguments));
        Assert.Equal(2, _items.GetContents(BackpackId()).Count);
    }

    [Fact]
    public async Task Buy_UnderTwoThousand_TheBankIsNotUsed()
    {
        Backpack();
        _bank.Gold[_player.Id] = 5000;
        var lines = await OpenAsync();

        await BuyAsync(Reply(_vendor.Id, (lines[1], 1)));

        Assert.Empty(_bank.Withdrawn);
        Assert.Equal(500192, _speech.ToldClilocs.Single().Cliloc);
    }

    [Fact]
    public async Task Buy_TheBankLacksTheFunds_RefusesAndChangesNothing()
    {
        Backpack();
        _bank.Gold[_player.Id] = 100;
        var lines = await OpenAsync();

        await BuyAsync(Reply(_vendor.Id, (lines[1], 2)));

        Assert.Equal(500191, _speech.ToldClilocs.Single().Cliloc);
        Assert.Empty(_bank.Withdrawn);
        Assert.Empty(_bank.Taken);
        Assert.Empty(_items.GetContents(BackpackId()));
        Assert.Single(_fixture.Sender.Sent.OfType<VendorEndPacket>());
    }

    [Fact]
    public async Task Buy_AGameMaster_PaysNothing()
    {
        Backpack();
        await _fixture.Network.ExecuteOnLoopAsync(() => _session.Set(SessionKeys.AccountType, AccountType.GameMaster));
        var lines = await OpenAsync();

        await BuyAsync(Reply(_vendor.Id, (lines[1], 2)));

        Assert.Equal(2, _items.GetContents(BackpackId()).Count);
        Assert.Empty(_bank.Taken);
        Assert.Empty(_speech.ToldClilocs);
    }

    [Fact]
    public async Task Buy_WithTooFewSerialsForThePieces_IsRefused_AndNothingMoves()
    {
        Backpack();
        _bank.Carried[_player.Id] = 5000;
        var lines = await OpenAsync();
        while (_serials.Serials.Count > 2)
        {
            _serials.Serials.Dequeue();
        }

        await BuyAsync(Reply(_vendor.Id, (lines[1], 3)));

        Assert.Equal(500187, _speech.ToldClilocs.Single().Cliloc);
        Assert.Empty(_bank.Taken);
        Assert.Empty(_items.GetContents(BackpackId()));
        Assert.Equal(2, _serials.Serials.Count);
    }

    [Fact]
    public async Task Buy_WithNoBackpack_PutsTheGoodsAtTheFeet()
    {
        _bank.Carried[_player.Id] = 100;
        var lines = await OpenAsync();

        await BuyAsync(Reply(_vendor.Id, (lines[0], 1)));

        Assert.Single(_view.Calls, call => call.StartsWith("Appeared"));
        Assert.Equal(8, Assert.Single(_bank.Taken).Amount);
    }

    [Fact]
    public async Task Buy_MoreThanTheStock_OrALineThatIsNotInTheWindow_EndsTheWindowAndChangesNothing()
    {
        Backpack();
        _bank.Carried[_player.Id] = 100000;
        var lines = await OpenAsync();

        await BuyAsync(Reply(_vendor.Id, (lines[0], 21)));

        Assert.Empty(_bank.Taken);
        Assert.Single(_fixture.Sender.Sent.OfType<VendorEndPacket>());

        lines = await OpenAsync();
        await BuyAsync(Reply(_vendor.Id, (new Serial(0x7FFF0000), 1)));

        Assert.Empty(_bank.Taken);
        Assert.Empty(_items.GetContents(BackpackId()));
    }

    [Fact]
    public async Task Buy_ForAnotherVendor_OrWithoutAWindow_OrWithMoreThanAHundredLines_IsDropped()
    {
        Backpack();
        _bank.Carried[_player.Id] = 100000;

        await BuyAsync(Reply(_vendor.Id, (new Serial(0x7FFF0000), 1)));
        Assert.Empty(_fixture.Sender.Sent.OfType<VendorEndPacket>());

        var lines = await OpenAsync();
        await BuyAsync(Reply(new Serial(999), (lines[0], 1)));
        await BuyAsync(Reply(_vendor.Id, Enumerable.Repeat((lines[0], 1), 101).ToArray()));

        Assert.Empty(_fixture.Sender.Sent.OfType<VendorEndPacket>());
        Assert.Empty(_bank.Taken);
        Assert.NotNull(_session.Get(VendorSessionKeys.Window));
    }

    [Fact]
    public async Task Buy_AfterTheVendorIsFarAway_OrOutOfSight_EndsTheWindowAndChangesNothing()
    {
        Backpack();
        _bank.Carried[_player.Id] = 100;
        var lines = await OpenAsync();
        _player.Location = new Point3D(40, 10, 0);

        await BuyAsync(Reply(_vendor.Id, (lines[0], 1)));

        Assert.Empty(_bank.Taken);
        Assert.Single(_fixture.Sender.Sent.OfType<VendorEndPacket>());
        Assert.Null(_session.Get(VendorSessionKeys.Window));
    }

    [Fact]
    public async Task Buy_ACancel_EndsTheWindow()
    {
        var lines = await OpenAsync();

        await BuyAsync(Reply(_vendor.Id, 0, (lines[0], 1)));

        Assert.Single(_fixture.Sender.Sent.OfType<VendorEndPacket>());
        Assert.Null(_session.Get(VendorSessionKeys.Window));
        Assert.Empty(_bank.Taken);
    }

    [Fact]
    public async Task OnSessionClosed_ForgetsTheWindow_AndALaterReplyIsDropped()
    {
        _bank.Carried[_player.Id] = 100;
        var lines = await OpenAsync();

        await OnLoopAsync(
            () =>
            {
                _vendors.OnSessionClosed(_session);

                return true;
            }
        );
        await BuyAsync(Reply(_vendor.Id, (lines[0], 1)));

        Assert.Null(_session.Get(VendorSessionKeys.Window));
        Assert.Empty(_bank.Taken);
        Assert.Empty(_fixture.Sender.Sent.OfType<VendorEndPacket>());
    }

    // The bank says Ok to a withdrawal unless a test says otherwise.
    [Fact]
    public async Task Buy_ABankThatRefusesTheWithdrawal_RefusesThePurchase()
    {
        Backpack();
        _bank.Gold[_player.Id] = 5000;
        _bank.Result = BankResultType.Busy;
        var lines = await OpenAsync();

        await BuyAsync(Reply(_vendor.Id, (lines[1], 2)));

        Assert.Equal(500191, _speech.ToldClilocs.Single().Cliloc);
        Assert.Empty(_bank.Taken);
    }

    public async Task DisposeAsync()
    {
        await _fixture.DisposeAsync();
    }

    // Opens the window and gives the virtual serial of each line, in the order of the shop.
    private async Task<Serial[]> OpenAsync()
    {
        Assert.True(await OnLoopAsync(() => _vendors.OpenBuy(_session, _vendor)));
        var lines = _fixture.Sender.Sent.OfType<ContainerContentPacket>().Last().Items;
        _fixture.Sender.Sent.Clear();

        return lines.OrderBy(line => line.GridX).Select(line => line.Serial).ToArray();
    }

    private async Task BuyAsync(VendorBuyReplyPacket packet)
    {
        await OnLoopAsync(
            () =>
            {
                _vendors.Buy(_session, packet);

                return true;
            }
        );
    }

    private static VendorBuyReplyPacket Reply(Serial vendor, params (Serial Line, int Amount)[] lines)
    {
        return Reply(vendor, VendorBuyReplyPacket.BuyFlag, lines);
    }

    private static VendorBuyReplyPacket Reply(Serial vendor, byte flag, params (Serial Line, int Amount)[] lines)
    {
        var bytes = new List<byte> { 0x3B, 0, 0 };
        bytes.AddRange(BitConverter.GetBytes(System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(vendor.Value)));
        bytes.Add(flag);

        foreach (var (line, amount) in lines)
        {
            bytes.Add(0x1A);
            bytes.AddRange(BitConverter.GetBytes(System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(line.Value)));
            bytes.AddRange(BitConverter.GetBytes(System.Buffers.Binary.BinaryPrimitives.ReverseEndianness((ushort)amount)));
        }

        bytes[1] = (byte)(bytes.Count >> 8);
        bytes[2] = (byte)bytes.Count;
        Assert.True(VendorBuyReplyPacket.TryParse(bytes.ToArray(), out var packet));

        return packet;
    }

    private ItemEntity Backpack()
    {
        var backpack = new ItemEntity { Id = new Serial(_nextItem++), TemplateId = "backpack", ItemId = 0x0E75, Amount = 1 };
        backpack.Equip(_player.Id, LayerType.Backpack);
        _items.Add([backpack]);

        return backpack;
    }

    private Serial BackpackId()
    {
        return _items.GetWorn(_player.Id).Single(item => item.Layer == LayerType.Backpack).Id;
    }

    private async Task<bool> OnLoopAsync(Func<bool> action)
    {
        var result = false;
        await _fixture.Network.ExecuteOnLoopAsync(() => result = action());

        return result;
    }
}
