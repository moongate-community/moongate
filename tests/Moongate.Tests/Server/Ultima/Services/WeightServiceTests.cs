using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Data.Items;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Tests.TestSupport.Ultima.Tiles;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class WeightServiceTests : IAsyncLifetime
{
    private const int BackpackGraphic = 0x0E75;
    private const int BagGraphic = 0x0E76;
    private const int CoinGraphic = 0x0EED;
    private const int DaggerGraphic = 0x0F52;
    private const int AnvilGraphic = 0x0FAF;

    private static readonly Serial Aria = new(2);

    private readonly FakeTileDataService _tiles = new FakeTileDataService()
        .Item(BackpackGraphic, TileFlagType.Container, 0, 3)
        .Item(BagGraphic, TileFlagType.Container, 0, 2)
        .Item(DaggerGraphic, TileFlagType.None, 0, 1)
        .Item(AnvilGraphic, TileFlagType.None, 0, 255);

    private readonly ItemEntity _backpack = Item(0x40000001, BackpackGraphic);
    private readonly ItemEntity _bag = Item(0x40000002, BagGraphic, "small_bag");
    private readonly ItemEntity _coins = Item(0x40000003, CoinGraphic, "gold");
    private readonly ItemEntity _dagger = Item(0x40000004, DaggerGraphic);
    private readonly ItemEntity _shirt = Item(0x40000005, DaggerGraphic);
    private readonly ItemEntity _bank = Item(0x40000006, BackpackGraphic);
    private readonly ItemEntity _ingots = Item(0x40000007, DaggerGraphic);
    private readonly ItemEntity _loose = Item(0x40000008, DaggerGraphic);

    private BroadcastFixture _fixture = null!;
    private ItemService _items = null!;
    private WeightService _weight = null!;
    private MobileEntity _aria = null!;

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        await _fixture.AddAsync(2);
        Assert.True(_fixture.Mobiles.TryGet(Aria, out _aria!));
        _items = TestItems.Create(_fixture.Sectors);
        _backpack.Equip(Aria, LayerType.Backpack);
        _shirt.Equip(Aria, LayerType.Shirt);
        _bank.Equip(Aria, LayerType.Bank);
        _bag.PutInContainer(_backpack.Id, new Point2D(50, 50));
        _coins.PutInContainer(_bag.Id, new Point2D(30, 30));
        _dagger.PutInContainer(_backpack.Id, new Point2D(60, 60));
        _ingots.PutInContainer(_bank.Id, new Point2D(60, 60));
        _ingots.Amount = 500;
        _coins.Amount = 70;
        _items.Add([_backpack, _bag, _coins, _dagger, _shirt, _bank, _ingots, _loose]);
        var templates = new ItemTemplateService(
            new StubDataLoaderService().With(
                new ItemTemplate { Id = "gold", Weight = 0.02m },
                new ItemTemplate { Id = "boulder", Weight = 500m },
                new ItemTemplate { Id = "small_bag", MaxWeight = 10 },
                new ItemTemplate { Id = "bottomless", MaxWeight = 0 }
            )
        );
        _weight = new(_items, templates, _tiles, _fixture.Sessions);
    }

    [Fact]
    public void Holds_BatchCountsAllNewItemsTogether()
    {
        Moongate.Server.Ultima.Interfaces.IWeightService weights = _weight;
        Assert.True(weights.Holds(_bag, Pile(0x40000080, 5)));
        Assert.False(weights.Holds(_bag, new[] { Pile(0x40000080, 5), Pile(0x40000081, 5) }));
    }

    [Fact]
    public void Of_APileIsItsUnitTimesItsAmountRoundedUp_AndAContainerAddsWhatIsInside()
    {
        // 70 coins of 0.02: 1.4, two stones, as the tooltip says. The bag weighs 2 by tiledata.
        Assert.Equal((2, 4, 1), (_weight.Of(_coins), _weight.Of(_bag), _weight.Of(_dagger)));
        // The backpack 3, the bag with its coins 4, the dagger 1.
        Assert.Equal(8, _weight.Of(_backpack));
    }

    [Fact]
    public void Of_WhatTiledataSaysCannotBeLifted_WeighsNothing()
    {
        Assert.Equal(0, _weight.Of(Item(0x40000020, AnvilGraphic)));
    }

    [Fact]
    public void Of_AHeavyTemplate_KeepsItsWeight_OnlyTiledatasCannotLiftIsNoWeight()
    {
        Assert.Equal(500, _weight.Of(Item(0x40000021, DaggerGraphic, "boulder")));
    }

    [Fact]
    public async Task Carried_CountsTheItemLiftedOutOfTheBank()
    {
        Assert.True(_fixture.Sessions.TryGetByCharacterId(Aria, out var session));

        await _fixture.Network.ExecuteOnLoopAsync(() => session!.Set(ItemSessionKeys.Held, new(_ingots.Id)));

        // The 500 ingots are in the hand now, though their row still says the bank.
        Assert.Equal(509, _weight.Carried(_aria));
    }

    [Fact]
    public void Holds_InAContainerAlreadyOverItsLimit_ItsOwnItemsStillMoveAround()
    {
        // Put there by a script: the backpack is over its 400 stones.
        var heavy = Pile(0x40000050, 450);
        heavy.PutInContainer(_backpack.Id, new Point2D(80, 80));
        _items.Add([heavy]);

        // To another spot of the backpack, and into the bag inside it, which has room for a stone.
        Assert.True(_weight.Holds(_backpack, _dagger));
        Assert.True(_weight.Holds(_bag, _dagger));
        // Nothing new comes in.
        Assert.False(_weight.Holds(_backpack, _loose));
        Assert.False(_weight.Holds(_bag, _loose));
    }

    [Fact]
    public void Carried_IsWhatTheMobileWearsWithItsContents_WithoutTheBank()
    {
        // The backpack and all in it 8, the shirt 1; 500 ingots lie in the bank.
        Assert.Equal(9, _weight.Carried(_aria));
    }

    [Fact]
    public async Task Carried_CountsTheItemLiftedFromTheGround_ButOnceTheOneLiftedFromThePack()
    {
        Assert.True(_fixture.Sessions.TryGetByCharacterId(Aria, out var session));

        await _fixture.Network.ExecuteOnLoopAsync(() => session!.Set(ItemSessionKeys.Held, new(_loose.Id)));
        Assert.Equal(10, _weight.Carried(_aria));

        await _fixture.Network.ExecuteOnLoopAsync(() => session!.Set(ItemSessionKeys.Held, new(_dagger.Id)));
        Assert.Equal(9, _weight.Carried(_aria));
    }

    [Fact]
    public void MaxCarried_CountsAStrengthBonus()
    {
        _aria.Strength = 100;
        var without = _weight.MaxCarried(_aria);
        _aria.StrengthBonus = 20;

        Assert.Equal(without + 70, _weight.MaxCarried(_aria));
    }

    [Theory, InlineData(100, 390), InlineData(25, 127), InlineData(0, 40)]
    public void MaxCarried_IsFortyStonesAndThreeAndAHalfAPointOfStrength(int strength, int stones)
    {
        _aria.Strength = strength;

        Assert.Equal(stones, _weight.MaxCarried(_aria));
    }

    [Fact]
    public void Holds_UpToTheLimitOfTheContainersTemplate_AndNoFurther()
    {
        // The small bag takes 10 stones and has 2 of coins.
        var eight = Pile(0x40000030, 8);
        var nine = Pile(0x40000031, 9);

        Assert.True(_weight.Holds(_bag, eight));
        Assert.False(_weight.Holds(_bag, nine));
    }

    [Fact]
    public void Holds_AnItemAlreadyInside_IsNotCountedTwice()
    {
        var eight = Pile(0x40000030, 8);
        eight.PutInContainer(_bag.Id, new Point2D(40, 40));
        _items.Add([eight]);

        // Moved to another spot of the same bag: 2 of coins and its own 8.
        Assert.True(_weight.Holds(_bag, eight));
    }

    [Fact]
    public void Holds_WithoutALimitInTheTemplate_FourHundredStones_AndEveryContainerAroundIsAsked()
    {
        // The backpack has no template: 400 stones, of which 5 are taken (the bag with its coins, the dagger).
        Assert.True(_weight.Holds(_backpack, Pile(0x40000030, 395)));
        Assert.False(_weight.Holds(_backpack, Pile(0x40000031, 396)));

        // Into a bag with no limit of its own, inside the backpack: the backpack still says no.
        var bottomless = Item(0x40000032, BagGraphic, "bottomless");
        bottomless.PutInContainer(_backpack.Id, new Point2D(70, 70));
        _items.Add([bottomless]);

        Assert.True(_weight.Holds(bottomless, Pile(0x40000033, 393)));
        Assert.False(_weight.Holds(bottomless, Pile(0x40000034, 394)));
    }

    [Fact]
    public void Holds_TheBankBox_AndWhatIsInIt_HaveNoLimit()
    {
        var chest = Item(0x40000040, BagGraphic);
        chest.PutInContainer(_bank.Id, new Point2D(40, 40));
        _items.Add([chest]);

        Assert.True(_weight.Holds(_bank, Pile(0x40000041, 5000)));
        // The chest itself keeps its own 400.
        Assert.True(_weight.Holds(chest, Pile(0x40000042, 400)));
        Assert.False(_weight.Holds(chest, Pile(0x40000043, 401)));
    }

    private static ItemEntity Pile(uint serial, int stones)
    {
        var pile = Item(serial, DaggerGraphic);
        pile.Amount = stones;

        return pile;
    }

    private static ItemEntity Item(uint serial, int graphic, string template = "item")
    {
        return new() { Id = new(serial), TemplateId = template, ItemId = graphic, Amount = 1 };
    }

    public async Task DisposeAsync()
    {
        await _fixture.DisposeAsync();
    }
}
