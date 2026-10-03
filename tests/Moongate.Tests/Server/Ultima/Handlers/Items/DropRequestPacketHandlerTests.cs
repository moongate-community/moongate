using Moongate.Tests.TestSupport.Ultima.Bank;
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
using Moongate.Server.Ultima.Types.Templates;
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

public sealed class DropRequestPacketHandlerTests : IAsyncDisposable
{
    private const int BackpackGraphic = 0x0E75;
    private const int BagGraphic = 0x0E76;
    private const int CoinGraphic = 0x0EED;

    private static readonly Serial Aria = new(0x00000002);
    private static readonly Serial Bran = new(0x00000003);
    private static readonly Serial Ground = new(0xFFFFFFFF);

    private readonly StubLineOfSightService _sight = new();
    private readonly RecordingWorldViewService _view = new();
    private readonly ItemService _items;
    private readonly MobileService _mobiles;
    private readonly StubPacketSendService _sender = new StubPacketSendService().Ignore<PropertyListInfoPacket>();
    private readonly FakeTileDataService _tiles = new FakeTileDataService()
                                                  .Item(BackpackGraphic, TileFlagType.Container, 0)
                                                  .Item(BagGraphic, TileFlagType.Container, 0)
                                                  .Item(CoinGraphic, TileFlagType.Generic, 0);

    private readonly ItemEntity _backpack = Item(0x40000001, BackpackGraphic);
    private readonly ItemEntity _bag = Item(0x40000002, BagGraphic);
    private readonly ItemEntity _innerBag = Item(0x40000003, BagGraphic);
    private readonly ItemEntity _coins = Item(0x40000004, CoinGraphic);
    private readonly ItemEntity _dagger = Item(0x40000005, 0x0F52);
    private readonly ItemEntity _otherBackpack = Item(0x40000006, BackpackGraphic);
    private readonly ItemEntity _pile = Item(0x40000007, CoinGraphic);
    private readonly ItemEntity _shirt = Item(0x40000008, 0x1517);
    private readonly StubBankService _bank = new();
    private readonly RecordingItemScriptService _scripts = new();

    private SessionFixture _fixture = null!;
    private GameSession _session = null!;

    public DropRequestPacketHandlerTests()
    {
        var sectors = TestSectors.Create();
        _items = TestItems.Create(sectors, new StubMovementService { DropZ = 0 }, _sight);
        _mobiles = new(new StubMovementService(), sectors);
        _mobiles.EnterWorld(new() { Id = Aria, Name = "Aria", Map = MapType.Trammel, Location = new Point3D(1496, 1628, 0) });
        _backpack.Equip(Aria, LayerType.Backpack);
        _bag.PutInContainer(_backpack.Id, new Point2D(50, 50));
        _innerBag.PutInContainer(_bag.Id, new Point2D(30, 30));
        _coins.PutInContainer(_backpack.Id, new Point2D(44, 65));
        _dagger.PutInContainer(_backpack.Id, new Point2D(100, 90));
        _otherBackpack.Equip(Bran, LayerType.Backpack);
        _coins.Amount = 30;
        _pile.Amount = 70;
        _pile.PutInContainer(_backpack.Id, new Point2D(120, 100));
        _shirt.Equip(Aria, LayerType.Shirt);
        _items.Add([_backpack, _bag, _innerBag, _coins, _dagger, _otherBackpack, _pile, _shirt]);
    }

    [Fact]
    public async Task Handle_IntoTheBackpackAtAPosition_MovesTheItemAndShowsItThere()
    {
        await HoldingAsync(_coins);

        await DropAsync(_coins.Id, 80, 70, _backpack.Id);

        AssertAt(_coins, _backpack.Id, new Point2D(80, 70));
    }

    [Fact]
    public async Task Handle_IntoTheBackpackAtAGridSlot_KeepsTheSlotTheClientAskedFor()
    {
        await HoldingAsync(_coins);

        await DropAsync(_coins.Id, 80, 70, _backpack.Id, 9);

        // The Enhanced Client shows the item in this slot, and is told so in the update it gets back.
        Assert.Equal((short)9, _coins.GridIndex);
        Assert.Equal((byte)9, _sender.Sent.OfType<ContainerItemUpdatePacket>().Last().Item.GridIndex);
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
    public async Task Handle_OntoAStackOfTheSameKind_MergesIntoItAndRemovesTheHeldOne()
    {
        await HoldingAsync(_coins);

        await DropAsync(_coins.Id, 0, 0, _pile.Id);

        Assert.Equal(100, _pile.Amount);
        Assert.False(_items.TryGet(_coins.Id, out _));
        Assert.Equal([_coins.Id], _items.TombstonesOf(Aria));
        Assert.Equal([typeof(ContainerItemUpdatePacket), typeof(RemoveEntityPacket)], _sender.Sent.Select(packet => packet.GetType()));
        Assert.Equal((_pile.Id, 100), (((ContainerItemUpdatePacket)_sender.Sent[0]).Item.Serial, ((ContainerItemUpdatePacket)_sender.Sent[0]).Item.Amount));
        Assert.Equal(_coins.Id, ((RemoveEntityPacket)_sender.Sent[1]).Serial);
        Assert.Null(_session.Get(ItemSessionKeys.Held));
    }

    [Fact]
    public async Task Handle_OntoAStackOfAnotherHue_IsPlacedBesideIt()
    {
        await HoldingAsync(_coins);
        _pile.Hue = new(0x0481);

        await DropAsync(_coins.Id, 0, 0, _pile.Id);

        AssertAt(_coins, _backpack.Id, new Point2D(120, 100));
        Assert.Equal((30, 70), (_coins.Amount, _pile.Amount));
    }

    [Theory]
    [InlineData("template")]
    [InlineData("name")]
    [InlineData("rarity")]
    [InlineData("props")]
    public async Task Handle_OntoAStackThatDiffersBeyondItsGraphic_IsPlacedBesideIt(string difference)
    {
        await HoldingAsync(_coins);

        switch (difference)
        {
            case "template":
                _pile.TemplateId = "rare_gold";
                break;
            case "name":
                _pile.Name = "cursed gold";
                break;
            case "rarity":
                _pile.Rarity = ItemRarityType.Rare;
                break;
            default:
                _pile.SetProp("minted", 3);
                break;
        }

        await DropAsync(_coins.Id, 0, 0, _pile.Id);

        AssertAt(_coins, _backpack.Id, new Point2D(120, 100));
        Assert.Equal((30, 70), (_coins.Amount, _pile.Amount));
    }

    [Fact]
    public async Task Handle_OntoAStackThatWouldPass60000_IsPlacedBesideIt()
    {
        await HoldingAsync(_coins);
        _pile.Amount = 59_980;

        await DropAsync(_coins.Id, 0, 0, _pile.Id);

        AssertAt(_coins, _backpack.Id, new Point2D(120, 100));
        Assert.Empty(_items.TombstonesOf(Aria));
    }

    [Fact]
    public async Task Handle_OntoAContainer_NeverMerges()
    {
        await HoldingAsync(_coins);

        await DropAsync(_coins.Id, 80, 70, _backpack.Id);

        AssertAt(_coins, _backpack.Id, new Point2D(80, 70));
        Assert.Equal(70, _pile.Amount);
    }

    [Theory, InlineData("backpack"), InlineData("bag"), InlineData("pile"), InlineData("ground")]
    public async Task Handle_ADrop_QueuesOnDropWithTheDropper(string where)
    {
        _scripts.Scripted.Add(_coins.TemplateId);
        await HoldingAsync(_coins);

        switch (where)
        {
            case "backpack":
                await DropAsync(_coins.Id, 80, 70, _backpack.Id);
                break;
            case "bag":
                await DropAsync(_coins.Id, 60, 70, _bag.Id);
                break;
            case "pile":
                await DropAsync(_coins.Id, 0, 0, _pile.Id);
                break;
            default:
                await DropAsync(_coins.Id, 1497, 1628, Ground);
                break;
        }

        Assert.Equal([$"0x{_coins.Id.Value:X8} on_drop 2"], _scripts.Queued);
    }

    [Theory, InlineData("backpack"), InlineData("bag"), InlineData("pile"), InlineData("ground")]
    public async Task Handle_ADropTheItemsScriptRefuses_BouncesBack_AndAsksNothingElse(string where)
    {
        _scripts.Scripted.Add(_coins.TemplateId);
        _scripts.Refused.Add("can_drop");
        await HoldingAsync(_coins);

        await DropTo(where);

        AssertAt(_coins, _backpack.Id, new Point2D(44, 65));
        Assert.Equal((30, 70), (_coins.Amount, _pile.Amount));
        Assert.Equal([$"0x{_coins.Id.Value:X8} can_drop 2"], _scripts.Calls);
        Assert.Empty(_scripts.Queued);
    }

    [Theory, InlineData("backpack", 0x40000001), InlineData("bag", 0x40000002), InlineData("pile", 0x40000001)]
    public async Task Handle_ADropTheContainersScriptRefuses_BouncesBack_AndAsksItOnce(string where, uint container)
    {
        _scripts.Scripted.Add(_coins.TemplateId);
        _scripts.Refused.Add("can_insert");
        await HoldingAsync(_coins);

        await DropTo(where);

        AssertAt(_coins, _backpack.Id, new Point2D(44, 65));
        Assert.Equal((30, 70), (_coins.Amount, _pile.Amount));
        Assert.Equal(
            [$"0x{_coins.Id.Value:X8} can_drop 2", $"0x{container:X8} can_insert 2 {_coins.Id.Value}"],
            _scripts.Calls
        );
        Assert.Empty(_scripts.Queued);
    }

    [Fact]
    public async Task Handle_ADropOnTheGround_AsksNoContainer()
    {
        _scripts.Scripted.Add(_coins.TemplateId);
        _scripts.Refused.Add("can_insert");
        await HoldingAsync(_coins);

        await DropTo("ground");

        Assert.NotNull(_coins.GroundLocation);
        Assert.Equal([$"0x{_coins.Id.Value:X8} can_drop 2"], _scripts.Calls);
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task Handle_IntoAChestOnTheGroundWhoseScriptRefuses_BouncesBack(bool ontoAnItemInside)
    {
        var (chest, ruby) = GroundChest(1497);
        _scripts.Scripted.Add(_coins.TemplateId);
        _scripts.Refused.Add("can_insert");
        await HoldingAsync(_coins);

        await DropAsync(_coins.Id, 60, 70, ontoAnItemInside ? ruby.Id : chest.Id);

        Assert.Equal(_backpack.Id, _coins.ContainerId);
        Assert.Equal(
            [$"0x{_coins.Id.Value:X8} can_drop 2", $"0x{chest.Id.Value:X8} can_insert 2 {_coins.Id.Value}"],
            _scripts.Calls
        );
    }

    [Fact]
    public async Task Handle_WhileTheScriptsAreAsked_TheItemIsStillHeld_SoTheyCannotDeleteIt()
    {
        var held = new List<Serial?>();
        _scripts.Scripted.Add(_coins.TemplateId);
        _scripts.OnRun = _ => held.Add(_session.Get(ItemSessionKeys.Held)?.Item);
        await HoldingAsync(_coins);

        await DropTo("bag");

        Assert.Equal([_coins.Id, _coins.Id], held);
        Assert.Null(_session.Get(ItemSessionKeys.Held));
        Assert.Equal(_bag.Id, _coins.ContainerId);
    }

    [Fact]
    public async Task Handle_AContainerItsScriptRemovesWhileAsked_ReceivesNothing()
    {
        _scripts.Scripted.Add(_coins.TemplateId);
        _scripts.OnRun = function =>
        {
            if (function == "can_insert")
            {
                _items.Remove([_innerBag.Id, _bag.Id]);
            }
        };
        await HoldingAsync(_coins);

        await DropTo("bag");

        Assert.Equal(_backpack.Id, _coins.ContainerId);
        Assert.Null(_session.Get(ItemSessionKeys.Held));
    }

    [Fact]
    public async Task Handle_OntoAPileInAChestOnTheGroundWhoseScriptRefuses_BouncesBack_AndAsksItOnce()
    {
        var (chest, _) = GroundChest(1497);
        var pile = Item(0x40000022, CoinGraphic);
        pile.Amount = 5;
        pile.PutInContainer(chest.Id, new Point2D(40, 40));
        _items.Add([pile]);
        _scripts.Scripted.Add(_coins.TemplateId);
        _scripts.Refused.Add("can_insert");
        await HoldingAsync(_coins);

        await DropAsync(_coins.Id, 0, 0, pile.Id);

        Assert.Equal((_backpack.Id, 30, 5), (_coins.ContainerId!.Value, _coins.Amount, pile.Amount));
        Assert.Equal(
            [$"0x{_coins.Id.Value:X8} can_drop 2", $"0x{chest.Id.Value:X8} can_insert 2 {_coins.Id.Value}"],
            _scripts.Calls
        );
    }

    [Fact]
    public async Task Handle_ADropTheRulesRefuse_DoesNotAskTheContainer()
    {
        _scripts.Scripted.Add(_coins.TemplateId);
        await HoldingAsync(_coins);

        await DropAsync(_coins.Id, 60, 70, _otherBackpack.Id);

        Assert.Equal([$"0x{_coins.Id.Value:X8} can_drop 2"], _scripts.Calls);
    }

    [Fact]
    public async Task Handle_ADropThatBounces_QueuesNothing()
    {
        _scripts.Scripted.Add(_bag.TemplateId);
        await HoldingAsync(_bag);

        await DropAsync(_bag.Id, 60, 70, _bag.Id);

        Assert.Empty(_scripts.Queued);
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

    [Theory, InlineData(false), InlineData(true)]
    public async Task Handle_IntoAChestOnTheGroundNearby_PutsItThere_ReleasesIt_AndTellsThoseAround(bool ontoAnItemInside)
    {
        var (chest, ruby) = GroundChest(1497);
        await HoldingAsync(_coins);

        await DropAsync(_coins.Id, 60, 70, ontoAnItemInside ? ruby.Id : chest.Id);

        Assert.Equal((Serial?)chest.Id, _coins.ContainerId);
        Assert.Equal([_coins], _items.GetContents(chest.Id).Where(item => item != ruby));
        // Its row still says the character carries it: the character's leave saves where it lies now.
        Assert.Equal([_coins], _items.TakeReleasedOf(Aria));
        Assert.Equal([$"ContainedAppeared {_coins.Id.Value} in {chest.Id.Value} except {Aria.Value}"], _view.Calls);
        Assert.Equal(_coins.Id, Assert.IsType<ContainerItemUpdatePacket>(Assert.Single(_sender.Sent)).Item.Serial);
        Assert.Null(_session.Get(ItemSessionKeys.Held));
    }

    [Fact]
    public async Task Handle_OntoAPileOfTheSameKindInAChestOnTheGround_GrowsThePile()
    {
        var (chest, _) = GroundChest(1497);
        var gold = Item(0x40000030, CoinGraphic);
        gold.Amount = 70;
        gold.PutInContainer(chest.Id, new Point2D(30, 30), 1);
        _items.Add([gold]);
        await HoldingAsync(_coins);

        await DropAsync(_coins.Id, 0, 0, gold.Id);

        Assert.Equal(100, gold.Amount);
        Assert.False(_items.TryGet(_coins.Id, out _));
        // The character's leave deletes the row of the coins and saves the pile that grew.
        Assert.Equal([_coins.Id], _items.TombstonesOf(Aria));
        Assert.Equal([gold], _items.TakeReleasedOf(Aria));
        Assert.Equal([$"ContainedAppeared {gold.Id.Value} in {chest.Id.Value} except {Aria.Value}"], _view.Calls);
        Assert.Equal(
            [typeof(ContainerItemUpdatePacket), typeof(RemoveEntityPacket)],
            _sender.Sent.Select(packet => packet.GetType())
        );
        Assert.Equal(gold.Id, ((ContainerItemUpdatePacket)_sender.Sent[0]).Item.Serial);
    }

    [Fact]
    public async Task Handle_OntoAPileInAChestOnTheGroundTooFar_BouncesBack()
    {
        var (chest, _) = GroundChest(1499);
        var gold = Item(0x40000030, CoinGraphic);
        gold.Amount = 70;
        gold.PutInContainer(chest.Id, new Point2D(30, 30), 1);
        _items.Add([gold]);
        await HoldingAsync(_coins);

        await DropAsync(_coins.Id, 0, 0, gold.Id);

        Assert.Equal((70, 30), (gold.Amount, _coins.Amount));
        Assert.Equal((Serial?)_backpack.Id, _coins.ContainerId);
    }

    [Fact]
    public async Task Handle_OntoAPileOfAnotherKindInAChestOnTheGround_LiesBesideIt()
    {
        var (chest, ruby) = GroundChest(1497);
        await HoldingAsync(_coins);

        await DropAsync(_coins.Id, 0, 0, ruby.Id);

        Assert.Equal((Serial?)chest.Id, _coins.ContainerId);
        Assert.Equal(30, _coins.Amount);
    }

    [Fact]
    public async Task Handle_IntoABagInsideAChestOnTheGround_PutsItThere()
    {
        var (chest, _) = GroundChest(1497);
        _items.MoveToContainer(_innerBag, chest.Id, new Point2D(10, 10));
        await HoldingAsync(_coins);

        await DropAsync(_coins.Id, 60, 70, _innerBag.Id);

        Assert.Equal((Serial?)_innerBag.Id, _coins.ContainerId);
        Assert.Equal([$"ContainedAppeared {_coins.Id.Value} in {chest.Id.Value} except {Aria.Value}"], _view.Calls);
    }

    [Fact]
    public async Task Handle_IntoAChestOnTheGroundTooFar_BouncesBack()
    {
        var (chest, _) = GroundChest(1499);
        await HoldingAsync(_coins);

        await DropAsync(_coins.Id, 60, 70, chest.Id);

        Assert.Equal((Serial?)_backpack.Id, _coins.ContainerId);
        Assert.Empty(_items.TakeReleasedOf(Aria));
        Assert.Empty(_view.Calls);
    }

    [Fact]
    public async Task Handle_IntoAChestOnTheGroundOutOfSightOrHeldBySomeone_BouncesBack()
    {
        var (chest, _) = GroundChest(1497);
        _sight.Allow = false;
        await HoldingAsync(_coins);

        await DropAsync(_coins.Id, 60, 70, chest.Id);

        Assert.Equal((Serial?)_backpack.Id, _coins.ContainerId);

        _sight.Allow = true;
        _items.Hide(chest);
        await _fixture.ExecuteOnLoopAsync(() => _session.Set(ItemSessionKeys.Held, new(_coins.Id)));
        await DropAsync(_coins.Id, 60, 70, chest.Id);

        Assert.Equal((Serial?)_backpack.Id, _coins.ContainerId);
    }

    // Those around were told it left the chest when it was lifted.
    [Fact]
    public async Task Handle_AnItemHeldFromAChestThatBouncesBack_IsShownAgainToThoseAround()
    {
        var (chest, ruby) = GroundChest(1497);
        await HoldingAsync(ruby);

        await DropAsync(ruby.Id, 60, 70, _otherBackpack.Id);

        Assert.Equal((Serial?)chest.Id, ruby.ContainerId);
        Assert.Equal([$"ContainedAppeared {ruby.Id.Value} in {chest.Id.Value} except {Aria.Value}"], _view.Calls);
        Assert.Equal(ruby.Id, Assert.IsType<ContainerItemUpdatePacket>(Assert.Single(_sender.Sent)).Item.Serial);
    }

    [Fact]
    public async Task Handle_IntoAFullChestOnTheGround_BouncesBack()
    {
        var (chest, _) = GroundChest(1497);

        for (var index = 0; index < 124; index++)
        {
            var filler = Item(0x40001000u + (uint)index, 0x0F13);
            filler.PutInContainer(chest.Id, new Point2D(10, 10), (byte)(index + 1));
            _items.Add([filler]);
        }

        await HoldingAsync(_coins);

        await DropAsync(_coins.Id, 60, 70, chest.Id);

        Assert.Equal((Serial?)_backpack.Id, _coins.ContainerId);
        Assert.Equal(125, _items.GetContents(chest.Id).Count);
    }

    [Fact]
    public async Task Handle_AChestOnTheGroundIntoItself_BouncesBack()
    {
        var (chest, ruby) = GroundChest(1497);
        _items.Hide(chest);
        await HoldingAsync(chest);

        await DropAsync(chest.Id, 60, 70, ruby.Id);

        Assert.Null(chest.ContainerId);
    }

    [Fact]
    public async Task Handle_IntoAnotherCharactersBackpack_StillBouncesBack()
    {
        await HoldingAsync(_coins);

        await DropAsync(_coins.Id, 60, 70, _otherBackpack.Id);

        Assert.Equal((Serial?)_backpack.Id, _coins.ContainerId);
        Assert.Empty(_view.Calls);
    }

    [Fact]
    public async Task Handle_OnTheGroundNearby_LaysItThereAndShowsIt()
    {
        await HoldingAsync(_coins);

        await DropAsync(_coins.Id, 1497, 1628, Ground);

        Assert.Equal((MapType.Trammel, new Point3D(1497, 1628, 0)), (_coins.Map!.Value, _coins.GroundLocation!.Value));
        Assert.Null(_coins.ContainerId);
        Assert.Equal([$"Appeared {_coins.Id.Value}"], _view.Calls);
        Assert.Empty(_sender.Sent);
        Assert.Null(_session.Get(ItemSessionKeys.Held));
    }

    [Fact]
    public async Task Handle_OnTheGround_IsReleasedByTheCharacterForItsLeaveSave()
    {
        await HoldingAsync(_coins);

        await DropAsync(_coins.Id, 1497, 1628, Ground);

        Assert.Equal([_coins], _items.TakeReleasedOf(Aria));
    }

    [Fact]
    public async Task Handle_OntoAGroundStack_QueuesTheDeletionAndReleasesTheStackForTheCharacter()
    {
        _items.PlaceOnGround(_pile, MapType.Trammel, new Point3D(1497, 1628, 0));
        await HoldingAsync(_coins);

        await DropAsync(_coins.Id, 0, 0, _pile.Id);

        Assert.Equal([_coins.Id], _items.TombstonesOf(Aria));
        Assert.Equal([_pile], _items.TakeReleasedOf(Aria));
    }

    [Fact]
    public async Task Handle_AHeldGroundItemOntoACarriedStack_QueuesTheDeletionForTheCharacter()
    {
        _items.PlaceOnGround(_pile, MapType.Trammel, new Point3D(1497, 1628, 0));
        _items.Hide(_pile);
        await HoldingAsync(_pile);

        await DropAsync(_pile.Id, 0, 0, _coins.Id);

        Assert.Equal(100, _coins.Amount);
        Assert.Equal([_pile.Id], _items.TombstonesOf(Aria));
    }

    [Fact]
    public async Task Handle_OnTheGroundTooFar_BouncesBackIntoTheContainer()
    {
        await HoldingAsync(_coins);

        await DropAsync(_coins.Id, 1500, 1628, Ground);

        AssertAt(_coins, _backpack.Id, new Point2D(44, 65));
        Assert.Empty(_view.Calls);
    }

    [Fact]
    public async Task Handle_OntoAStack_SendsTheGrownStacksTooltipRevision()
    {
        await HoldingAsync(_coins);

        await DropAsync(_coins.Id, 0, 0, _pile.Id);

        Assert.Equal(_pile.Id, Assert.IsType<PropertyListInfoPacket>(Assert.Single(_sender.Ignored)).Serial);
    }

    [Fact]
    public async Task Handle_AWornItemThatCannotBeDropped_GoesBackOnTheWearer()
    {
        await HoldingAsync(_shirt);

        await DropAsync(_shirt.Id, 1500, 1628, Ground);

        Assert.Equal((Aria, LayerType.Shirt), (_shirt.MobileId!.Value, _shirt.Layer!.Value));
        Assert.Equal([$"Worn {Aria.Value} {_shirt.Id.Value}"], _view.Calls);
        Assert.Empty(_sender.Sent);
    }

    [Fact]
    public async Task Handle_ASerialThatIsNotTheHeldItem_BouncesTheHeldItem()
    {
        await HoldingAsync(_shirt);

        await DropAsync(_dagger.Id, 60, 70, _backpack.Id);

        Assert.Equal((Aria, LayerType.Shirt), (_shirt.MobileId!.Value, _shirt.Layer!.Value));
        Assert.Equal([$"Worn {Aria.Value} {_shirt.Id.Value}"], _view.Calls);
    }

    [Fact]
    public async Task Handle_AWornItemIntoTheBackpack_IsNoLongerWorn()
    {
        await HoldingAsync(_shirt);

        await DropAsync(_shirt.Id, 60, 70, _backpack.Id);

        Assert.Equal(_backpack.Id, _shirt.ContainerId);
        Assert.Null(_shirt.MobileId);
        Assert.DoesNotContain(_shirt, _items.GetWorn(Aria));
        // Anyone who came into range while it was held still saw it on the character.
        Assert.Equal([$"Unworn {Aria.Value} {_shirt.Id.Value}"], _view.Calls);
    }

    [Fact]
    public async Task Handle_OnTheGroundOutOfSight_BouncesBackIntoTheContainer()
    {
        _sight.Allow = false;
        await HoldingAsync(_coins);

        await DropAsync(_coins.Id, 1497, 1628, Ground);

        AssertAt(_coins, _backpack.Id, new Point2D(44, 65));
    }

    [Fact]
    public async Task Handle_AHeldGroundItemThatBounces_IsShownAgainWhereItLies()
    {
        _items.PlaceOnGround(_pile, MapType.Trammel, new Point3D(1497, 1628, 0));
        _items.Hide(_pile);
        await HoldingAsync(_pile);

        await DropAsync(_pile.Id, 1500, 1628, Ground);

        Assert.Equal(new Point3D(1497, 1628, 0), _pile.GroundLocation);
        Assert.Equal([$"Appeared {_pile.Id.Value}"], _view.Calls);
        Assert.Empty(_sender.Sent);
    }

    [Fact]
    public async Task Handle_AHeldGroundItemIntoTheBackpack_GoesThere()
    {
        _items.PlaceOnGround(_pile, MapType.Trammel, new Point3D(1497, 1628, 0));
        _items.Hide(_pile);
        await HoldingAsync(_pile);

        await DropAsync(_pile.Id, 80, 70, _backpack.Id);

        AssertAt(_pile, _backpack.Id, new Point2D(80, 70));
        Assert.Null(_pile.GroundLocation);
    }

    [Fact]
    public async Task Handle_OntoAGroundStackNearby_MergesAndRefreshesIt()
    {
        _items.PlaceOnGround(_pile, MapType.Trammel, new Point3D(1497, 1628, 0));
        await HoldingAsync(_coins);

        await DropAsync(_coins.Id, 0, 0, _pile.Id);

        Assert.Equal(100, _pile.Amount);
        Assert.False(_items.TryGet(_coins.Id, out _));
        Assert.Equal([$"Appeared {_pile.Id.Value}"], _view.Calls);
        Assert.Equal(_coins.Id, Assert.IsType<RemoveEntityPacket>(Assert.Single(_sender.Sent)).Serial);
    }

    [Fact]
    public async Task Handle_OntoAGroundStackTooFar_Bounces()
    {
        _items.PlaceOnGround(_pile, MapType.Trammel, new Point3D(1500, 1628, 0));
        await HoldingAsync(_coins);

        await DropAsync(_coins.Id, 0, 0, _pile.Id);

        Assert.Equal(70, _pile.Amount);
        AssertAt(_coins, _backpack.Id, new Point2D(44, 65));
    }

    [Fact]
    public async Task Handle_IntoAClosedBank_Bounces()
    {
        await HoldingAsync(_coins);
        _bank.Locked.Add(_bag.Id);

        await DropAsync(_coins.Id, 60, 70, _bag.Id);

        AssertAt(_coins, _backpack.Id, new Point2D(44, 65));
    }

    [Fact]
    public async Task Handle_OntoAStackInAClosedBank_Bounces()
    {
        await HoldingAsync(_coins);
        _bank.Locked.Add(_pile.Id);

        await DropAsync(_coins.Id, 0, 0, _pile.Id);

        Assert.Equal(70, _pile.Amount);
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
    public async Task Handle_AnotherItemThanTheHeldOne_MovesNothingAndPutsTheHeldOneBack()
    {
        await HoldingAsync(_coins);

        await DropAsync(_dagger.Id, 60, 70, _bag.Id);

        Assert.Equal(_coins.Id, Assert.IsType<ContainerItemUpdatePacket>(Assert.Single(_sender.Sent)).Item.Serial);
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

    // A bag on the ground of Aria's row at the given x, with a ruby inside.
    private (ItemEntity Chest, ItemEntity Ruby) GroundChest(int x)
    {
        var chest = Item(0x40000020, BagGraphic);
        var ruby = Item(0x40000021, 0x0F13);
        ruby.PutInContainer(chest.Id, new Point2D(20, 20));
        _items.Add([chest, ruby]);
        _items.PlaceOnGround(chest, MapType.Trammel, new Point3D(x, 1628, 0));

        return (chest, ruby);
    }

    private Task DropTo(string where)
    {
        return where switch
        {
            "backpack" => DropAsync(_coins.Id, 80, 70, _backpack.Id),
            "bag"      => DropAsync(_coins.Id, 60, 70, _bag.Id),
            "pile"     => DropAsync(_coins.Id, 0, 0, _pile.Id),
            _          => DropAsync(_coins.Id, 1497, 1628, Ground)
        };
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

    private Task DropAsync(Serial item, short x, short y, Serial destination, byte gridIndex = 0)
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
        var handler = new DropRequestPacketHandler(_items, _mobiles, _view, _tiles, layouts, _sender, TestTooltips.Create(_items, _mobiles), _scripts, _bank);
        var packet = new DropRequestPacket { Item = item, X = x, Y = y, Z = 0, GridIndex = gridIndex, Destination = destination };

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
