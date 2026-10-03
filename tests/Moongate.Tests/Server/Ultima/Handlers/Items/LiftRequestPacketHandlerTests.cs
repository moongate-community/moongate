using Moongate.Tests.TestSupport.Ultima.Bank;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Services.Sessions;
using Moongate.Server.Ultima.Data.Internal.Items;
using Moongate.Server.Ultima.Data.Items;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Handlers.Items;
using Moongate.Server.Ultima.Packets.General;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Types.Items;
using Moongate.Tests.Support.Sessions;
using Moongate.Tests.TestSupport.Packets;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Tests.TestSupport.Ultima.Sectors;
using Moongate.Tests.TestSupport.Ultima.Tiles;
using Moongate.Tests.TestSupport.Ultima.Tooltips;
using Moongate.Tests.TestSupport.Ultima.World;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Handlers.Items;

public sealed class LiftRequestPacketHandlerTests : IAsyncDisposable
{
    private static readonly Serial Aria = new(0x00000002);
    private static readonly Serial Bran = new(0x00000003);

    private readonly StubLineOfSightService _sight = new();
    private readonly RecordingWorldViewService _view = new();
    private readonly ItemService _items;
    private readonly MobileService _mobiles;
    private readonly ItemEntity _groundGold = Item(0x40000007, 100);
    private readonly RecordingItemScriptService _scripts = new();
    private readonly StubPacketSendService _sender = new StubPacketSendService().Ignore<PropertyListInfoPacket>();
    private readonly ItemEntity _backpack = Item(0x40000001, 1);
    private readonly ItemEntity _coins = Item(0x40000002, 250);
    private readonly ItemEntity _dagger = Item(0x40000003, 1);
    private readonly ItemEntity _otherBackpack = Item(0x40000004, 1);
    private readonly ItemEntity _otherDagger = Item(0x40000005, 1);
    private readonly ItemEntity _bolts = new() { Id = new(0x40000006), TemplateId = "bolts", ItemId = 0x1BFB, Amount = 10 };
    private readonly ItemEntity _shirt = new() { Id = new(0x40000008), TemplateId = "shirt", ItemId = 0x1517, Amount = 1 };
    private readonly ItemEntity _otherShirt = new() { Id = new(0x40000009), TemplateId = "shirt", ItemId = 0x1517, Amount = 1 };
    private readonly StubItemSerialPool _pool = new();
    private readonly StubBankService _bank = new();
    private readonly ItemTemplateService _templates = new(
        new StubDataLoaderService().With(new ItemTemplate { Id = "statue", ItemId = new Serial(0x0EED), Movable = false })
    );
    private const int ChestGraphic = 0x0E41;

    private readonly FakeTileDataService _tiles = new FakeTileDataService()
                                                  .Item(0x0EED, TileFlagType.Generic, 0)
                                                  .Item(ChestGraphic, TileFlagType.Container, 0, weight: 255);

    private SessionFixture _fixture = null!;
    private GameSession _session = null!;
    private SessionService _sessions = null!;

    public LiftRequestPacketHandlerTests()
    {
        var sectors = TestSectors.Create();
        _items = TestItems.Create(sectors, sight: _sight);
        _mobiles = new(new StubMovementService(), sectors);
        _mobiles.EnterWorld(new() { Id = Aria, Name = "Aria", Map = MapType.Trammel, Location = new Point3D(1496, 1628, 0) });
        _backpack.Equip(Aria, LayerType.Backpack);
        _coins.PutInContainer(_backpack.Id, new Point2D(44, 65));
        _dagger.PutInContainer(_backpack.Id, new Point2D(60, 80));
        _otherBackpack.Equip(Bran, LayerType.Backpack);
        _otherDagger.PutInContainer(_otherBackpack.Id, new Point2D(60, 80));
        _bolts.PutInContainer(_backpack.Id, new Point2D(70, 90));
        _shirt.Equip(Aria, LayerType.Shirt);
        _otherShirt.Equip(Bran, LayerType.Shirt);
        _items.Add([_backpack, _coins, _dagger, _otherBackpack, _otherDagger, _bolts, _shirt, _otherShirt]);
        _pool.Serials.Enqueue(new Serial(0x40000100));
        _items.Add([_groundGold]);
        _items.PlaceOnGround(_groundGold, MapType.Trammel, new Point3D(1497, 1628, 0));
    }

    [Fact]
    public async Task Handle_AWornItemOfTheCharacter_LiftsItAndTakesItOffForTheOthers()
    {
        await StartAsync(Aria);

        await LiftAsync(_shirt.Id, 1);

        Assert.Equal(new HeldItem(_shirt.Id), _session.Get(ItemSessionKeys.Held));
        Assert.Equal([$"Unworn {Aria.Value} {_shirt.Id.Value}"], _view.Calls);
        Assert.Empty(_sender.Sent);
        // Worn until it is dropped somewhere.
        Assert.Equal((Aria, LayerType.Shirt), (_shirt.MobileId!.Value, _shirt.Layer!.Value));
    }

    [Fact]
    public async Task Handle_PartOfAWornStack_IsRefused()
    {
        // Splitting it would leave two items on one layer, which no save can write.
        var torches = new ItemEntity { Id = new(0x4000000A), TemplateId = "torch", ItemId = 0x0EED, Amount = 5 };
        torches.Equip(Aria, LayerType.TwoHanded);
        _items.Add([torches]);
        await StartAsync(Aria);

        await LiftAsync(torches.Id, 1);

        Assert.Null(_session.Get(ItemSessionKeys.Held));
        Assert.Equal(5, torches.Amount);
        Assert.Single(_items.GetWorn(Aria), item => item.Layer == LayerType.TwoHanded);
    }

    [Fact]
    public async Task Handle_AnItemInAClosedBank_IsRefusedAndShownBack()
    {
        await StartAsync(Aria);
        _bank.Locked.Add(_dagger.Id);

        await LiftAsync(_dagger.Id, 1);

        Assert.Null(_session.Get(ItemSessionKeys.Held));
        AssertRefused(LiftRejectReasonType.CannotLift, _dagger);
    }

    [Fact]
    public async Task Handle_TheBankBox_IsRefusedAndShownBackOnTheCharacter()
    {
        var box = Item(0x4000000A, 1);
        box.Equip(Aria, LayerType.Bank);
        _items.Add([box]);
        await StartAsync(Aria);

        await LiftAsync(box.Id, 1);

        Assert.Null(_session.Get(ItemSessionKeys.Held));
        Assert.Equal([typeof(LiftRejectPacket), typeof(WornItemPacket)], _sender.Sent.Select(packet => packet.GetType()));
    }

    [Fact]
    public async Task Handle_TheBackpack_IsRefusedAndShownBackOnTheCharacter()
    {
        await StartAsync(Aria);

        await LiftAsync(_backpack.Id, 1);

        Assert.Null(_session.Get(ItemSessionKeys.Held));
        // As ModernUO: the reject, then the equip update that puts it back on the paperdoll.
        Assert.Equal([typeof(LiftRejectPacket), typeof(WornItemPacket)], _sender.Sent.Select(packet => packet.GetType()));
        Assert.Equal(LiftRejectReasonType.CannotLift, ((LiftRejectPacket)_sender.Sent[0]).Reason);
        Assert.Equal(_backpack.Id, ((WornItemPacket)_sender.Sent[1]).Item);
    }

    [Fact]
    public async Task Handle_ALift_QueuesOnPickupWithThePicker_ASecondWhileHoldingDoesNot()
    {
        _scripts.Scripted.Add("shirt");
        _scripts.Scripted.Add("item");
        await StartAsync(Aria);

        await LiftAsync(_shirt.Id, 1);
        await LiftAsync(_groundGold.Id, 100);

        Assert.Equal(["0x40000008 on_pickup 2"], _scripts.Queued);
    }

    [Fact]
    public async Task Handle_PartOfAStack_QueuesOnPickupOfTheLiftedPartOnly()
    {
        _scripts.Scripted.Add("item");
        await StartAsync(Aria);

        await LiftAsync(_coins.Id, 50);

        Assert.Equal(["0x40000002 on_pickup 2"], _scripts.Queued);
    }

    [Fact]
    public async Task Handle_AGroundItemLifted_QueuesOnPickup()
    {
        _scripts.Scripted.Add("item");
        await StartAsync(Aria);

        await LiftAsync(_groundGold.Id, 100);

        Assert.Equal(["0x40000007 on_pickup 2"], _scripts.Queued);
    }

    [Fact]
    public async Task Handle_ALiftTheItemsScriptRefuses_IsRejectedBeforeAnythingMoves()
    {
        _scripts.Scripted.Add("item");
        _scripts.Refused.Add("can_pick_up");
        await StartAsync(Aria);

        await LiftAsync(_coins.Id, 50);

        // Inspecific shows no message of the client's: the script tells the player why.
        AssertRefused(LiftRejectReasonType.Inspecific, _coins);
        Assert.Equal(250, _coins.Amount);
        Assert.Null(_session.Get(ItemSessionKeys.Held));
        Assert.Equal(["0x40000002 can_pick_up 2"], _scripts.Calls);
        Assert.Empty(_scripts.Queued);
    }

    [Fact]
    public async Task Handle_ALiftTheItemsScriptAllows_AsksItOnceAndLifts()
    {
        _scripts.Scripted.Add("item");
        await StartAsync(Aria);

        await LiftAsync(_coins.Id, 250);

        Assert.Equal(_coins.Id, _session.Get(ItemSessionKeys.Held)!.Item);
        Assert.Equal(["0x40000002 can_pick_up 2"], _scripts.Calls);
    }

    [Fact]
    public async Task Handle_ALiftTheRulesRefuse_DoesNotAskTheScript()
    {
        _scripts.Scripted.Add("shirt");
        await StartAsync(Aria);

        await LiftAsync(_otherShirt.Id, 1);

        Assert.Empty(_scripts.Calls);
    }

    [Fact]
    public async Task Handle_ARefusedLift_QueuesNothing()
    {
        _scripts.Scripted.Add("shirt");
        await StartAsync(Aria);

        await LiftAsync(_otherShirt.Id, 1);

        Assert.Empty(_scripts.Queued);
    }

    [Fact]
    public async Task Handle_AnotherCharactersWornItem_IsRefusedWithoutShowingIt()
    {
        await StartAsync(Aria);

        await LiftAsync(_otherShirt.Id, 1);

        Assert.Null(_session.Get(ItemSessionKeys.Held));
        Assert.IsType<LiftRejectPacket>(Assert.Single(_sender.Sent));
        Assert.Empty(_view.Calls);
    }

    [Fact]
    public async Task Handle_AGroundItemNearby_LiftsItAndTakesItOffEveryScreen()
    {
        await StartAsync(Aria);

        await LiftAsync(_groundGold.Id, 100);

        Assert.Equal(new HeldItem(_groundGold.Id), _session.Get(ItemSessionKeys.Held));
        Assert.Equal([$"Disappeared {_groundGold.Id.Value}"], _view.Calls);
        Assert.Empty(_sender.Sent);
    }

    [Fact]
    public async Task Handle_PartOfAGroundStack_LeavesTheRestOnTheGround()
    {
        await StartAsync(Aria);

        await LiftAsync(_groundGold.Id, 40);

        Assert.True(_items.TryGet(new Serial(0x40000100), out var rest));
        Assert.Equal((60, _groundGold.GroundLocation), (rest.Amount, rest.GroundLocation));
        Assert.Equal([$"Appeared {rest.Id.Value}", $"Disappeared {_groundGold.Id.Value}"], _view.Calls);
        Assert.Empty(_sender.Sent);
    }

    [Theory, InlineData(1497, true), InlineData(1499, false)]
    public async Task Handle_AnItemInAChestOnTheGround_IsLiftedOnlyWithinReach(int x, bool reached)
    {
        var (chest, ruby) = GroundChest(x);
        await StartAsync(Aria);

        await LiftAsync(ruby.Id, 1);

        Assert.Equal(reached ? new HeldItem(ruby.Id) : null, _session.Get(ItemSessionKeys.Held));
        Assert.Equal(reached ? [] : [typeof(LiftRejectPacket)], _sender.Sent.Select(packet => packet.GetType()));
        // The chest stays where it is; those who look into it see the ruby go.
        Assert.Equal(reached ? [$"ContainedDisappeared {ruby.Id.Value} in {chest.Id.Value} except {Aria.Value}"] : [], _view.Calls);
        Assert.True(_items.IsLyingOnGround(chest));
    }

    [Fact]
    public async Task Handle_PartOfAPileInAChestOnTheGround_ShowsTheRestToThoseAround()
    {
        var (chest, ruby) = GroundChest(1497);
        ruby.Amount = 100;
        await StartAsync(Aria);

        await LiftAsync(ruby.Id, 40);

        Assert.True(_items.TryGet(new Serial(0x40000100), out var rest));
        Assert.Equal((60, (Serial?)chest.Id), (rest.Amount, rest.ContainerId));
        Assert.Equal(
            [
                $"ContainedAppeared {rest.Id.Value} in {chest.Id.Value} except {Aria.Value}",
                $"ContainedDisappeared {ruby.Id.Value} in {chest.Id.Value} except {Aria.Value}"
            ],
            _view.Calls
        );
    }

    // In a chest the item stays in place while it is held: a second hand must not take it too.
    [Fact]
    public async Task Handle_AnItemInAChestThatAnotherPlayerHolds_IsRefused()
    {
        var (_, ruby) = GroundChest(1497);
        await StartAsync(Aria);
        var other = _sessions.GetOrCreate(new Moongate.Tests.TestSupport.Network.ControlledNetworkConnection(77));
        await _fixture.ExecuteOnLoopAsync(() => other.Set(ItemSessionKeys.Held, new(ruby.Id)));

        await LiftAsync(ruby.Id, 1);

        Assert.Null(_session.Get(ItemSessionKeys.Held));
        Assert.Equal(LiftRejectReasonType.CannotLift, Assert.IsType<LiftRejectPacket>(Assert.Single(_sender.Sent)).Reason);
        Assert.Empty(_view.Calls);
    }

    [Fact]
    public async Task Handle_AnItemInABagInsideAChestOnTheGround_IsLifted()
    {
        var (chest, ruby) = GroundChest(1497);
        var bag = Item(0x40000022, 1);
        bag.PutInContainer(chest.Id, new Point2D(10, 10));
        _items.Add([bag]);
        _items.MoveToContainer(ruby, bag.Id, new Point2D(5, 5), 0);
        await StartAsync(Aria);

        await LiftAsync(ruby.Id, 1);

        Assert.Equal(new HeldItem(ruby.Id), _session.Get(ItemSessionKeys.Held));
    }

    [Fact]
    public async Task Handle_AnItemInAChestSomeoneHolds_IsRefused()
    {
        var (chest, ruby) = GroundChest(1497);
        _items.Hide(chest);
        await StartAsync(Aria);

        await LiftAsync(ruby.Id, 1);

        Assert.Null(_session.Get(ItemSessionKeys.Held));
        Assert.IsType<LiftRejectPacket>(Assert.Single(_sender.Sent));
    }

    [Fact]
    public async Task Handle_AnItemInAChestOutOfSight_IsRefused()
    {
        var (_, ruby) = GroundChest(1497);
        _sight.Allow = false;
        await StartAsync(Aria);

        await LiftAsync(ruby.Id, 1);

        Assert.Null(_session.Get(ItemSessionKeys.Held));
    }

    [Fact]
    public async Task Handle_AGroundItemMadeImmovable_IsRefusedAndShownAgain()
    {
        _groundGold.Movable = false;
        await StartAsync(Aria);

        await LiftAsync(_groundGold.Id, 100);

        Assert.Null(_session.Get(ItemSessionKeys.Held));
        Assert.Equal(LiftRejectReasonType.CannotLift, Assert.IsType<LiftRejectPacket>(Assert.Single(_sender.Sent)).Reason);
        Assert.Equal([$"ShownTo 2 {_groundGold.Id.Value}"], _view.Calls);
    }

    // As a treasure chest or a lamp post: the client's tiledata gives it the weight that cannot be lifted.
    [Fact]
    public async Task Handle_AGroundItemTooHeavyToLift_IsRefused()
    {
        var (chest, _) = GroundChest(1497);
        await StartAsync(Aria);

        await LiftAsync(chest.Id, 1);

        Assert.Null(_session.Get(ItemSessionKeys.Held));
        Assert.Equal(LiftRejectReasonType.CannotLift, Assert.IsType<LiftRejectPacket>(Assert.Single(_sender.Sent)).Reason);
        Assert.True(_items.IsLyingOnGround(chest));
    }

    [Fact]
    public async Task Handle_AGroundItemWhoseTemplateIsFixed_IsRefused()
    {
        var statue = new ItemEntity { Id = new(0x40000030), TemplateId = "statue", ItemId = 0x0EED, Amount = 1 };
        _items.Add([statue]);
        _items.PlaceOnGround(statue, MapType.Trammel, new Point3D(1497, 1628, 0));
        await StartAsync(Aria);

        await LiftAsync(statue.Id, 1);

        Assert.Null(_session.Get(ItemSessionKeys.Held));
        Assert.Equal(LiftRejectReasonType.CannotLift, Assert.IsType<LiftRejectPacket>(Assert.Single(_sender.Sent)).Reason);
    }

    [Fact]
    public async Task Handle_AnImmovableItemMadeMovable_IsLifted()
    {
        var (chest, _) = GroundChest(1497);
        chest.Movable = true;
        await StartAsync(Aria);

        await LiftAsync(chest.Id, 1);

        Assert.Equal(new HeldItem(chest.Id), _session.Get(ItemSessionKeys.Held));
    }

    [Theory, InlineData(AccountType.GameMaster), InlineData(AccountType.Administrator)]
    public async Task Handle_Staff_LiftsWhatCannotBeLifted(AccountType type)
    {
        _groundGold.Movable = false;
        await StartAsync(Aria);
        await _fixture.ExecuteOnLoopAsync(() => _session.Set(SessionKeys.AccountType, type));

        await LiftAsync(_groundGold.Id, 100);

        Assert.Equal(new HeldItem(_groundGold.Id), _session.Get(ItemSessionKeys.Held));
    }

    [Fact]
    public async Task Handle_AGroundItemTooFar_IsRefusedAndShownAgain()
    {
        _items.PlaceOnGround(_groundGold, MapType.Trammel, new Point3D(1499, 1628, 0));
        await StartAsync(Aria);

        await LiftAsync(_groundGold.Id, 100);

        Assert.Null(_session.Get(ItemSessionKeys.Held));
        Assert.Equal(LiftRejectReasonType.OutOfRange, Assert.IsType<LiftRejectPacket>(Assert.Single(_sender.Sent)).Reason);
        Assert.Equal([$"ShownTo 2 {_groundGold.Id.Value}"], _view.Calls);
    }

    [Fact]
    public async Task Handle_AGroundItemSomeoneHolds_IsRefusedWithoutShowingIt()
    {
        _items.Hide(_groundGold);
        await StartAsync(Aria);

        await LiftAsync(_groundGold.Id, 100);

        Assert.Null(_session.Get(ItemSessionKeys.Held));
        Assert.IsType<LiftRejectPacket>(Assert.Single(_sender.Sent));
        Assert.Empty(_view.Calls);
    }

    [Fact]
    public async Task Handle_AGroundItemOutOfSight_IsRefused()
    {
        _sight.Allow = false;
        await StartAsync(Aria);

        await LiftAsync(_groundGold.Id, 100);

        Assert.Null(_session.Get(ItemSessionKeys.Held));
        Assert.Equal(LiftRejectReasonType.OutOfSight, Assert.IsType<LiftRejectPacket>(Assert.Single(_sender.Sent)).Reason);
    }

    [Fact]
    public async Task Handle_AWholeItemInTheOwnBackpack_IsHeldAndNothingIsSent()
    {
        await StartAsync(Aria);

        await LiftAsync(_coins.Id, 250);

        Assert.Equal(new HeldItem(_coins.Id), _session.Get(ItemSessionKeys.Held));
        Assert.Empty(_sender.Sent);
        Assert.Equal((_backpack.Id, new Point2D(44, 65)), (_coins.ContainerId!.Value, _coins.GridLocation!.Value));
    }

    [Fact]
    public async Task Handle_WhileHoldingAnother_IsRefusedAndTheItemIsShownBack()
    {
        await StartAsync(Aria);
        await LiftAsync(_coins.Id, 250);

        await LiftAsync(_dagger.Id, 1);

        AssertRefused(LiftRejectReasonType.AreHolding, _dagger);
        Assert.Equal(new HeldItem(_coins.Id), _session.Get(ItemSessionKeys.Held));
    }

    [Fact]
    public async Task Handle_PartOfAStack_SplitsIt_TheHeldPartKeepsTheSerialAndTheRestIsShown()
    {
        await StartAsync(Aria);

        await LiftAsync(_coins.Id, 30);

        Assert.Equal(new HeldItem(_coins.Id), _session.Get(ItemSessionKeys.Held));
        Assert.Equal(30, _coins.Amount);
        Assert.True(_items.TryGet(new Serial(0x40000100), out var rest));
        Assert.Equal((220, _backpack.Id, new Point2D(44, 65)), (rest.Amount, rest.ContainerId!.Value, rest.GridLocation!.Value));
        var update = Assert.IsType<ContainerItemUpdatePacket>(Assert.Single(_sender.Sent));
        Assert.Equal((rest.Id, 220), (update.Item.Serial, update.Item.Amount));
        Assert.Equal(rest.Id, Assert.IsType<PropertyListInfoPacket>(Assert.Single(_sender.Ignored)).Serial);
    }

    [Fact]
    public async Task Handle_PartOfAStackWithNoSerialLeft_IsRefusedAndShownBack()
    {
        await StartAsync(Aria);
        _pool.Serials.Clear();

        await LiftAsync(_coins.Id, 30);

        AssertRefused(LiftRejectReasonType.Inspecific, _coins);
        Assert.Equal(250, _coins.Amount);
        Assert.Null(_session.Get(ItemSessionKeys.Held));
    }

    [Fact]
    public async Task Handle_PartOfAnItemThatDoesNotStack_IsRefused()
    {
        await StartAsync(Aria);

        await LiftAsync(_bolts.Id, 4);

        AssertRefused(LiftRejectReasonType.CannotLift, _bolts);
        Assert.Equal(10, _bolts.Amount);
    }

    [Theory, InlineData(0), InlineData(251)]
    public async Task Handle_AnAmountOutsideTheStack_IsRefused(int amount)
    {
        await StartAsync(Aria);

        await LiftAsync(_coins.Id, amount);

        AssertRefused(LiftRejectReasonType.CannotLift, _coins);
        Assert.Equal(250, _coins.Amount);
    }

    [Fact]
    public async Task Handle_AnotherCharactersItem_IsRefusedWithoutShowingIt()
    {
        await StartAsync(Aria);

        await LiftAsync(_otherDagger.Id, 1);

        Assert.Equal(LiftRejectReasonType.CannotLift, Assert.IsType<LiftRejectPacket>(Assert.Single(_sender.Sent)).Reason);
    }

    [Fact]
    public async Task Handle_AnotherCharactersItemWhileHolding_DoesNotShowIt()
    {
        await StartAsync(Aria);
        await LiftAsync(_coins.Id, 250);

        await LiftAsync(_otherDagger.Id, 1);

        Assert.Equal(LiftRejectReasonType.AreHolding, Assert.IsType<LiftRejectPacket>(Assert.Single(_sender.Sent)).Reason);
    }

    [Fact]
    public async Task Handle_AnItemThatIsNotLive_IsRefused()
    {
        await StartAsync(Aria);

        await LiftAsync(new Serial(0x40000099), 1);

        Assert.Equal(LiftRejectReasonType.CannotLift, Assert.IsType<LiftRejectPacket>(Assert.Single(_sender.Sent)).Reason);
    }

    [Fact]
    public async Task Handle_WithoutACharacter_IsRefused()
    {
        await StartAsync(null);

        await LiftAsync(_coins.Id, 250);

        Assert.IsType<LiftRejectPacket>(_sender.Sent[0]);
        Assert.Null(_session.Get(ItemSessionKeys.Held));
    }

    private void AssertRefused(LiftRejectReasonType reason, ItemEntity item)
    {
        Assert.Equal([typeof(LiftRejectPacket), typeof(ContainerItemUpdatePacket)], _sender.Sent.Select(packet => packet.GetType()));
        Assert.Equal(reason, ((LiftRejectPacket)_sender.Sent[0]).Reason);
        var update = (ContainerItemUpdatePacket)_sender.Sent[1];
        Assert.Equal((item.Id, item.ContainerId!.Value, item.GridX!.Value), (update.Item.Serial, update.Item.Container, (short)update.Item.GridX));
    }

    private async Task StartAsync(Serial? character)
    {
        _fixture = await SessionFixture.CreateAsync();
        _sessions = new SessionService(_fixture.Loop);
        _session = _sessions.GetOrCreate(_fixture.Client);

        if (character is { } id)
        {
            await _fixture.ExecuteOnLoopAsync(() => _session.Set(SessionKeys.CharacterId, id));
        }
    }

    private Task LiftAsync(Serial item, int amount)
    {
        var handler = new LiftRequestPacketHandler(_items, _mobiles, _view, _pool, _tiles, _sender, TestTooltips.Create(_items, _mobiles), _scripts, _bank, _templates, _sessions);

        return _fixture.ExecuteOnLoopAsync(() => handler.Handle(_session, new LiftRequestPacket { Item = item, Amount = amount }));
    }

    // A chest two rows from Aria at the given x, with a ruby inside.
    private (ItemEntity Chest, ItemEntity Ruby) GroundChest(int x)
    {
        var chest = new ItemEntity { Id = new(0x40000020), TemplateId = "chest", ItemId = ChestGraphic, Amount = 1 };
        var ruby = Item(0x40000021, 1);
        ruby.PutInContainer(chest.Id, new Point2D(20, 20));
        _items.Add([chest, ruby]);
        _items.PlaceOnGround(chest, MapType.Trammel, new Point3D(x, 1628, 0));

        return (chest, ruby);
    }

    private static ItemEntity Item(uint serial, int amount)
    {
        return new() { Id = new(serial), TemplateId = "item", ItemId = 0x0EED, Amount = amount };
    }

    public async ValueTask DisposeAsync()
    {
        if (_fixture is not null)
        {
            await _fixture.DisposeAsync();
        }
    }
}
