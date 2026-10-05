using Moongate.Server.Ultima.Interfaces;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Tests.TestSupport.Ultima.World;
using Moongate.Tests.TestSupport.Ultima.Weight;
using Moongate.Server.Ultima.Types.Bank;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Ultima.Data.Containers;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Packets.General;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Timing;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Tests.TestSupport.Ultima.Tiles;
using Moongate.Tests.TestSupport.Ultima.Tooltips;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class BankServiceTests : IAsyncLifetime
{
    private readonly ItemService _items = TestItems.Create();
    private readonly FakeItemFactoryService _factory;
    private readonly SettableClock _time = new() { Now = new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero) };

    private BroadcastFixture _fixture = null!;
    private GameSession _session = null!;
    private MobileEntity _aria = null!;
    private BankService _bank = null!;
    private readonly ItemTemplateService _templates;
    private readonly StubItemSerialPool _serials = new();
    private readonly StubWeightService _weight = new();
    private readonly RecordingFatigueService _fatigue = new();
    private readonly BankConfig _config = new();
    private ContainerCapacityService _capacity = null!;
    private ContainerLayoutService _layouts = null!;
    private uint _nextItem = 0x40001000;

    public BankServiceTests()
    {
        var templates = new ItemTemplateService(
            new StubDataLoaderService().With(
                new ItemTemplate { Id = BankService.BankTemplate, ItemId = new Serial(0x0E7C), Name = "bank box", Movable = false },
                new ItemTemplate { Id = "gold", ItemId = new Serial(0x0EED), Weight = 0.02m },
                new ItemTemplate { Id = "backpack", ItemId = new Serial(0x0E75), MaxWeight = 400 },
                new ItemTemplate { Id = "bag", ItemId = new Serial(0x0E76) },
                new ItemTemplate { Id = "sword", ItemId = new Serial(0x0F5E) }
            )
        );
        _templates = templates;
        _factory = new(templates, new FakeTileDataService().Item(0x0EED, TileFlagType.Generic, 0));
    }

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        _session = await _fixture.AddAsync(2);
        Assert.True(_fixture.Mobiles.TryGet(new Serial(2), out var aria));
        _aria = aria;
        _aria.Location = new Point3D(1600, 1600, 0);
        // A player: an NPC has no account, and no bank.
        _aria.AccountId = new Serial(1002);
        var layouts = new ContainerLayoutService(
            new StubDataLoaderService().With(new ContainerContent { Name = "metal chest", Gump = 0x004A, Items = [0x0E7C], Default = true })
        );
        _layouts = layouts;
        _capacity = new ContainerCapacityService(_items, _templates, _config);
        _bank = BankWith(_weight);

        for (uint index = 0; index < 32; index++)
        {
            _serials.Serials.Enqueue(new Serial(0x40002000 + index));
        }
    }

    public async Task DisposeAsync()
    {
        await _fixture.DisposeAsync();
    }

    [Fact]
    public async Task Open_TheFirstTime_CreatesTheBankBox_SavesIt_AndShowsIt()
    {
        Assert.True(await OnLoopAsync(() => _bank.Open(_aria)));
        await OnLoopAsync(() => true);

        var box = Assert.Single(_items.GetWorn(_aria.Id), item => item.Layer == LayerType.Bank);
        Assert.Equal(BankService.BankTemplate, box.TemplateId);
        Assert.Contains(_factory.Saved, saved => saved.Contains(box));
        Assert.Equal(box.Id, Assert.Single(_fixture.Sender.Sent.OfType<WornItemPacket>()).Item);
        Assert.Equal(0x004A, Assert.Single(_fixture.Sender.Sent.OfType<DisplayContainerPacket>()).Gump);
        Assert.Single(_fixture.Sender.Sent.OfType<ContainerContentPacket>());
        Assert.Contains(_fixture.Sender.Sent.OfType<UnicodeSpeechMessagePacket>(), message => message.Text == "Bank container has 0 items.");
        Assert.True(_bank.IsOpen(_aria));
    }

    [Fact]
    public async Task Open_ThePlayerLeavesBeforeTheBoxIsSaved_LeavesItToTheNextLogin()
    {
        await OnLoopAsync(
            () =>
            {
                _bank.Open(_aria);
                _fixture.Sessions.Remove(_session.SessionId);
                _fixture.Mobiles.LeaveWorld(_aria.Id);

                return true;
            }
        );
        await OnLoopAsync(() => true);

        Assert.Single(_factory.Saved);
        Assert.Empty(_items.GetWorn(_aria.Id));
        Assert.Empty(_fixture.Sender.Sent);
    }

    [Fact]
    public async Task Open_ThePlayerLogsInAgainBeforeTheBoxIsSaved_ShowsNothingToTheOldCharacter()
    {
        var again = new MobileEntity { Id = _aria.Id, Name = "Aria", AccountId = _aria.AccountId, Map = MapType.Trammel, Location = _aria.Location };
        await OnLoopAsync(
            () =>
            {
                _bank.Open(_aria);
                _fixture.Mobiles.EnterWorld(again);

                return true;
            }
        );
        await OnLoopAsync(() => true);

        Assert.Empty(_items.GetWorn(_aria.Id));
        Assert.Empty(_fixture.Sender.Sent);
        Assert.False(_bank.IsOpen(again));
    }

    [Fact]
    public async Task Open_AnExistingBox_ShowsWhatIsInIt()
    {
        var (_, coin) = AddBank();

        Assert.True(await OnLoopAsync(() => _bank.Open(_aria)));

        Assert.Empty(_factory.Saved);
        Assert.Contains(coin.Id, Assert.Single(_fixture.Sender.Sent.OfType<ContainerContentPacket>()).Items.Select(item => item.Serial));
        Assert.Contains(_fixture.Sender.Sent.OfType<UnicodeSpeechMessagePacket>(), message => message.Text == "Bank container has 1 items.");
    }

    [Fact]
    public async Task IsOpen_EndsWhenThePlayerStepsChangesMapOrLogsInAgain()
    {
        AddBank();
        await OnLoopAsync(() => _bank.Open(_aria));

        _aria.Location = new Point3D(1601, 1600, 0);
        Assert.False(_bank.IsOpen(_aria));

        await OnLoopAsync(() => _bank.Open(_aria));
        _aria.Map = MapType.Felucca;
        Assert.False(_bank.IsOpen(_aria));

        _aria.Map = MapType.Trammel;
        Assert.True(_bank.IsOpen(_aria));
        Assert.False(_bank.IsOpen(new MobileEntity { Id = _aria.Id, Map = MapType.Trammel, Location = _aria.Location }));
    }

    [Fact]
    public async Task Close_EndsIt_EvenBackOnTheSameSpot()
    {
        AddBank();
        await OnLoopAsync(() => _bank.Open(_aria));

        _bank.Close(_aria);

        Assert.False(_bank.IsOpen(_aria));
    }

    [Fact]
    public async Task ASessionClosing_EndsIt()
    {
        AddBank();
        await OnLoopAsync(() => _bank.Open(_aria));

        _bank.OnSessionClosed(_session);

        Assert.False(_bank.IsOpen(_aria));
    }

    [Fact]
    public async Task Open_AgainWithinASecond_ShowsTheBankOnce_AsManyBankersHearTheSameWord()
    {
        AddBank();

        await OnLoopAsync(() => _bank.Open(_aria) && _bank.Open(_aria));
        _time.Advance(TimeSpan.FromSeconds(1));
        await OnLoopAsync(() => _bank.Open(_aria));

        Assert.Equal(2, _fixture.Sender.Sent.OfType<DisplayContainerPacket>().Count());
    }

    [Fact]
    public async Task Open_APlayerWithoutASession_IsRefused()
    {
        var ghost = new MobileEntity { Id = new Serial(9), Map = MapType.Trammel };

        Assert.False(await OnLoopAsync(() => _bank.Open(ghost)));
        Assert.Empty(_fixture.Sender.Sent);
    }

    [Fact]
    public async Task CanAccess_TheBankOnlyWhileItIsOpen_ExceptStaff()
    {
        var (_, coin) = AddBank();
        var dagger = new ItemEntity { Id = new Serial(0x40000020), TemplateId = "dagger", ItemId = 0x0F51, Amount = 1 };
        dagger.PlaceOnGround(MapType.Trammel, new Point3D(1600, 1600, 0));
        _items.Add([dagger]);

        Assert.False(_bank.CanAccess(_session, _aria, coin));
        Assert.True(_bank.CanAccess(_session, _aria, dagger));

        await OnLoopAsync(() => _bank.Open(_aria));
        Assert.True(_bank.CanAccess(_session, _aria, coin));

        _aria.Location = new Point3D(1602, 1600, 0);
        await _fixture.Network.ExecuteOnLoopAsync(() => _session.Set(SessionKeys.AccountType, AccountType.GameMaster));
        Assert.True(_bank.CanAccess(_session, _aria, coin));
    }

    [Fact]
    public async Task CanAccess_SomeoneElsesBank_IsRefusedEvenOpen()
    {
        var (_, coin) = AddBank();
        var bob = await _fixture.AddAsync(3);
        Assert.True(_fixture.Mobiles.TryGet(new Serial(3), out var bobby));

        await OnLoopAsync(() => _bank.Open(_aria));

        Assert.False(_bank.CanAccess(bob, bobby, coin));
    }

    private (ItemEntity Box, ItemEntity Coin) AddBank()
    {
        var box = new ItemEntity { Id = new Serial(0x40000010), TemplateId = BankService.BankTemplate, ItemId = 0x0E7C, Amount = 1 };
        box.Equip(_aria.Id, LayerType.Bank);
        var coin = new ItemEntity { Id = new Serial(0x40000011), TemplateId = "gold", ItemId = 0x0EED, Amount = 5 };
        coin.PutInContainer(box.Id, new Point2D(44, 65));
        _items.Add([box, coin]);

        return (box, coin);
    }

    private async Task<bool> OnLoopAsync(Func<bool> action)
    {
        var result = false;
        await _fixture.Network.ExecuteOnLoopAsync(() => result = action());

        return result;
    }

    [Fact]
    public async Task Balance_IsTheGoldInTheBox_BagsIncluded_AndNothingElse()
    {
        var box = await BoxAsync();
        Gold(box, 1200);
        Gold(box, 300);
        Gold(In(box, "bag"), 4000);
        In(box, "sword");
        Gold(Backpack(), 999);

        Assert.Equal(5500, _bank.Balance(_aria));
    }

    [Fact]
    public void Balance_WithNoBankBoxYet_IsZero()
    {
        Assert.Equal(0, _bank.Balance(_aria));
    }

    [Fact]
    public async Task Withdraw_MovesTheCoinsToTheBackpack()
    {
        var box = await BoxAsync();
        var pile = Gold(box, 1200);
        var backpack = Backpack();

        Assert.Equal(BankResultType.Ok, _bank.Withdraw(_aria, 500));

        Assert.Equal(700, pile.Amount);
        var carried = Assert.Single(_items.GetContents(backpack.Id));
        Assert.Equal(("gold", 500), (carried.TemplateId, carried.Amount));
        Assert.Equal(700, _bank.Balance(_aria));
    }

    [Fact]
    public async Task Withdraw_TakesPileByPile_TheSmallestFirst_AndDeletesTheEmptyOnes()
    {
        var box = await BoxAsync();
        var large = Gold(box, 1000);
        var small = Gold(box, 200);
        var inBag = Gold(In(box, "bag"), 300);
        Backpack();

        Assert.Equal(BankResultType.Ok, _bank.Withdraw(_aria, 600));

        Assert.False(_items.TryGet(small.Id, out _));
        Assert.False(_items.TryGet(inBag.Id, out _));
        Assert.Equal(900, large.Amount);
    }

    [Fact]
    public async Task Withdraw_JoinsAGoldPileOfTheBackpack_WhenItFits()
    {
        Gold(await BoxAsync(), 5000);
        var carried = Gold(Backpack(), 100);

        Assert.Equal(BankResultType.Ok, _bank.Withdraw(_aria, 500));

        Assert.Equal(600, carried.Amount);
        Assert.Single(_items.GetContents(carried.ContainerId!.Value));
    }

    // A pile holds 60000: what would go past it makes a pile of its own, and no coin is lost.
    [Fact]
    public async Task Withdraw_NextToAPileThatIsNearlyFull_MakesANewPile()
    {
        Gold(await BoxAsync(), 5000);
        var backpack = Backpack();
        var carried = Gold(backpack, 59_800);

        Assert.Equal(BankResultType.Ok, _bank.Withdraw(_aria, 500));

        Assert.Equal(59_800, carried.Amount);
        Assert.Equal([500, 59_800], _items.GetContents(backpack.Id).Select(item => item.Amount).Order());
    }

    [Theory, InlineData(0), InlineData(-5)]
    public async Task Withdraw_AnAmountThatIsNone_IsRefused(int amount)
    {
        Gold(await BoxAsync(), 5000);
        Backpack();

        Assert.Equal(BankResultType.BadAmount, _bank.Withdraw(_aria, amount));
        Assert.Equal(5000, _bank.Balance(_aria));
    }

    [Fact]
    public async Task Withdraw_MoreThanABankerHandsOut_IsRefused()
    {
        _config.MaxWithdraw = 1000;
        Gold(await BoxAsync(), 5000);
        Backpack();

        Assert.Equal(BankResultType.TooMuch, _bank.Withdraw(_aria, 1001));
        Assert.Equal(BankResultType.Ok, _bank.Withdraw(_aria, 1000));
    }

    [Fact]
    public async Task Withdraw_MoreThanThereIs_IsRefused_AndNothingMoves()
    {
        var pile = Gold(await BoxAsync(), 400);
        var backpack = Backpack();

        Assert.Equal(BankResultType.NotEnoughGold, _bank.Withdraw(_aria, 401));

        Assert.Equal(400, pile.Amount);
        Assert.Empty(_items.GetContents(backpack.Id));
    }

    [Fact]
    public void Withdraw_WithNoBankBoxYet_SaysSo()
    {
        Backpack();

        Assert.Equal(BankResultType.NoBank, _bank.Withdraw(_aria, 100));
    }

    [Fact]
    public async Task Withdraw_IntoABackpackThatCannotHoldTheWeight_IsRefused_AndNothingMoves()
    {
        var pile = Gold(await BoxAsync(), 5000);
        var backpack = Backpack();
        _weight.HoldsResult = false;

        Assert.Equal(BankResultType.BackpackFull, _bank.Withdraw(_aria, 500));

        Assert.Equal(5000, pile.Amount);
        Assert.Empty(_items.GetContents(backpack.Id));
    }

    // As ModernUO: sixty thousand coins weigh three times what a backpack holds, and the banker hands them out all the
    // same. Its customer walks away overloaded.
    [Fact]
    public async Task Withdraw_MoreGoldThanTheBackpackHolds_IsHandedOutAllTheSame()
    {
        var bank = BankWith(RealWeights());
        Gold(await BoxAsync(), 60_000);
        var backpack = Backpack();

        Assert.Equal(BankResultType.Ok, bank.Withdraw(_aria, 60_000));

        Assert.Equal(60_000, Assert.Single(_items.GetContents(backpack.Id)).Amount);
        Assert.Equal(0, bank.Balance(_aria));
    }

    // Twenty thousand coins are the four hundred stones a backpack holds: it takes nothing more.
    [Fact]
    public async Task Withdraw_IntoABackpackAlreadyAtItsWeight_IsRefused_AndNothingMoves()
    {
        var bank = BankWith(RealWeights());
        var pile = Gold(await BoxAsync(), 5000);
        var carried = Gold(Backpack(), 20_000);

        Assert.Equal(BankResultType.BackpackFull, bank.Withdraw(_aria, 100));

        Assert.Equal((5000, 20_000), (pile.Amount, carried.Amount));
    }

    [Fact]
    public async Task Withdraw_WithoutABackpack_IsRefused()
    {
        Gold(await BoxAsync(), 5000);

        Assert.Equal(BankResultType.BackpackFull, _bank.Withdraw(_aria, 500));
        Assert.Equal(5000, _bank.Balance(_aria));
    }

    // No serial for the new pile: the gold stays where it was.
    [Fact]
    public async Task Withdraw_WhenNoSerialIsReady_IsBusy_AndNothingMoves()
    {
        var pile = Gold(await BoxAsync(), 5000);
        Backpack();
        _serials.Serials.Clear();

        Assert.Equal(BankResultType.Busy, _bank.Withdraw(_aria, 500));
        Assert.Equal(5000, pile.Amount);
    }

    [Fact]
    public async Task Withdraw_ShowsThePlayerItsBackpackGold()
    {
        Gold(await BoxAsync(), 5000);
        Backpack();
        var before = _fixture.Sender.Sent.Count;

        _bank.Withdraw(_aria, 500);

        Assert.Contains(_fixture.Sender.Sent.Skip(before).OfType<ContainerItemUpdatePacket>(), packet => packet.Item.Amount == 500);
    }

    // The gold weighs: the player's status shows its new load, and it is warned when it is now overloaded.
    [Fact]
    public async Task Withdraw_AndDeposit_ShowThePlayerItsWeightAgain()
    {
        Gold(await BoxAsync(), 5000);
        Backpack();

        _bank.Withdraw(_aria, 500);

        Assert.Equal((_aria, true), Assert.Single(_fatigue.Loads) switch { var load => (load.Mobile, load.Warn) });

        _bank.Deposit(_aria, 200);

        Assert.Equal(2, _fatigue.Loads.Count);
    }

    [Fact]
    public async Task ARefusedWithdrawal_ShowsNoWeight()
    {
        Gold(await BoxAsync(), 100);
        Backpack();

        _bank.Withdraw(_aria, 500);

        Assert.Empty(_fatigue.Loads);
    }

    [Fact]
    public async Task Deposit_MovesTheCoinsFromTheBackpackAndItsBags_IntoTheBox()
    {
        var box = await BoxAsync();
        var backpack = Backpack();
        var loose = Gold(backpack, 300);
        var inBag = Gold(In(backpack, "bag"), 500);

        Assert.Equal(BankResultType.Ok, _bank.Deposit(_aria, 600));

        Assert.Equal(600, _bank.Balance(_aria));
        Assert.Equal(600, Assert.Single(_items.GetContents(box.Id)).Amount);
        // The smaller pile went whole, the other gave the rest.
        Assert.False(_items.TryGet(loose.Id, out _));
        Assert.Equal(200, inBag.Amount);
    }

    [Fact]
    public async Task Deposit_TopsUpThePilesOfTheBox_ThenMakesPilesOfSixtyThousand()
    {
        var box = await BoxAsync();
        var there = Gold(box, 59_000);
        var backpack = Backpack();
        Gold(backpack, 60_000);
        Gold(backpack, 60_000);
        Gold(backpack, 5000);

        Assert.Equal(BankResultType.Ok, _bank.Deposit(_aria, 125_000));

        Assert.Equal(60_000, there.Amount);
        Assert.Equal([4000, 60_000, 60_000, 60_000], _items.GetContents(box.Id).Select(item => item.Amount).Order());
        Assert.Empty(_items.GetContents(backpack.Id));
    }

    [Fact]
    public async Task Deposit_MoreThanIsCarried_IsRefused_AndNothingMoves()
    {
        await BoxAsync();
        var carried = Gold(Backpack(), 400);

        Assert.Equal(BankResultType.NotEnoughGold, _bank.Deposit(_aria, 401));

        Assert.Equal(400, carried.Amount);
        Assert.Equal(0, _bank.Balance(_aria));
    }

    [Fact]
    public async Task Deposit_IntoAFullBox_IsRefused_AndNothingMoves()
    {
        _config.MaxItems = 2;
        var box = await BoxAsync();
        In(box, "sword");
        In(box, "sword");
        var carried = Gold(Backpack(), 400);

        Assert.Equal(BankResultType.BankFull, _bank.Deposit(_aria, 400));

        Assert.Equal(400, carried.Amount);
    }

    // The gold joins a pile that is there: a full box takes it.
    [Fact]
    public async Task Deposit_IntoAFullBoxWithAGoldPile_TopsItUp()
    {
        _config.MaxItems = 2;
        var box = await BoxAsync();
        In(box, "sword");
        var there = Gold(box, 100);
        Gold(Backpack(), 400);

        Assert.Equal(BankResultType.Ok, _bank.Deposit(_aria, 400));
        Assert.Equal(500, there.Amount);
    }

    [Fact]
    public async Task Deposit_WhenNoSerialIsReady_IsBusy_AndNothingMoves()
    {
        await BoxAsync();
        var carried = Gold(Backpack(), 400);
        _serials.Serials.Clear();

        Assert.Equal(BankResultType.Busy, _bank.Deposit(_aria, 400));
        Assert.Equal(400, carried.Amount);
    }

    [Theory, InlineData(0), InlineData(-1)]
    public async Task Deposit_AnAmountThatIsNone_IsRefused(int amount)
    {
        await BoxAsync();
        Gold(Backpack(), 400);

        Assert.Equal(BankResultType.BadAmount, _bank.Deposit(_aria, amount));
    }

    [Fact]
    public void Deposit_WithNoBankBoxYet_SaysSo()
    {
        Gold(Backpack(), 400);

        Assert.Equal(BankResultType.NoBank, _bank.Deposit(_aria, 100));
    }

    [Fact]
    public void AnNpc_HasNoBank()
    {
        var orc = new MobileEntity { Id = new Serial(900), Name = "an orc" };

        Assert.Null(_bank.Balance(orc));
        Assert.Equal(BankResultType.NoPlayer, _bank.Withdraw(orc, 10));
        Assert.Equal(BankResultType.NoPlayer, _bank.Deposit(orc, 10));
    }

    // The bank with the weights a test wants: the stub says yes to everything, the real one weighs the gold.
    private BankService BankWith(IWeightService weight)
    {
        var tooltips = TestTooltips.Create(_items, _fixture.Mobiles);
        var handling = new ItemHandlingService(_items, _fixture.Sessions, _fixture.Sender, new RecordingWorldViewService(), tooltips, _factory, _serials, _layouts, _capacity);

        return new(
            _items,
            _factory,
            _fixture.Sessions,
            _fixture.Mobiles,
            _fixture.Sender,
            tooltips,
            _layouts,
            _fixture.Network.Loop,
            handling,
            _capacity,
            weight,
            new ItemsConfig { GoldTemplate = "gold", BackpackTemplate = "backpack" },
            _config,
            fatigue: _fatigue,
            time: _time
        );
    }

    private WeightService RealWeights()
    {
        return new(_items, _templates, new FakeTileDataService().Item(0x0EED, TileFlagType.Generic, 0));
    }

    private async Task<ItemEntity> BoxAsync()
    {
        await OnLoopAsync(() => _bank.Open(_aria));
        await OnLoopAsync(() => true);

        return Assert.Single(_items.GetWorn(_aria.Id), item => item.Layer == LayerType.Bank);
    }

    private ItemEntity Backpack()
    {
        var backpack = new ItemEntity { Id = new Serial(_nextItem++), TemplateId = "backpack", ItemId = 0x0E75, Amount = 1 };
        backpack.Equip(_aria.Id, LayerType.Backpack);
        _items.Add([backpack]);

        return backpack;
    }

    private ItemEntity In(ItemEntity container, string template)
    {
        var item = new ItemEntity { Id = new Serial(_nextItem++), TemplateId = template, ItemId = 0x0E76, Amount = 1 };
        item.PutInContainer(container.Id, new Point2D(50, 50), 0);
        _items.Add([item]);

        return item;
    }

    private ItemEntity Gold(ItemEntity container, int amount)
    {
        var gold = In(container, "gold");
        gold.ItemId = 0x0EED;
        gold.Amount = amount;

        return gold;
    }
}
