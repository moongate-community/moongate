using Moongate.Server.Ultima.Interfaces;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Data.Internal.Items;
using Moongate.Server.Ultima.Data.Items;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Packets.General;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Ultima.Containers;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Tests.TestSupport.Ultima.Tiles;
using Moongate.Tests.TestSupport.Ultima.Tooltips;
using Moongate.Tests.TestSupport.Ultima.World;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class ItemHandlingServiceTests : IAsyncLifetime
{
    private readonly ItemService _items = TestItems.Create();
    private readonly StubItemSerialPool _serials = new();
    private readonly RecordingWorldViewService _view = new();

    private readonly FakeTileDataService _tiles = new FakeTileDataService()
        .Item(0x0EED, TileFlagType.Generic, 0)
        .Item(0x0F5E, TileFlagType.Weapon, 0)
        .Item(0x0E75, TileFlagType.Container, 0);

    private readonly ItemTemplateService _templates = new(
        new StubDataLoaderService().With(
            new ItemTemplate { Id = "gold", ItemId = new Serial(0x0EED) },
            new ItemTemplate { Id = "sword", ItemId = new Serial(0x0F5E) },
            new ItemTemplate { Id = "backpack", ItemId = new Serial(0x0E75) }
        )
    );

    private readonly ItemEntity _backpack = new()
        { Id = new Serial(0x40000001), TemplateId = "backpack", ItemId = 0x0E75, Amount = 1 };

    private readonly ItemEntity _gold = new()
        { Id = new Serial(0x40000002), TemplateId = "gold", ItemId = 0x0EED, Amount = 100 };

    private BroadcastFixture _fixture = null!;
    private MobileEntity _owner = null!;
    private ItemHandlingService _handling = null!;

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        await _fixture.AddAsync(2);
        Assert.True(_fixture.Mobiles.TryGet(new Serial(2), out _owner!));
        _backpack.Equip(_owner.Id, LayerType.Backpack);
        _gold.PutInContainer(_backpack.Id, new Point2D(20, 20));
        _items.Add([_backpack, _gold]);
        _handling = Handling();
    }

    [Fact]
    public void Give_MakesTheItemInTheBackpack_AndShowsItToTheOwner()
    {
        _serials.Serials.Enqueue(new Serial(0x40000F00));

        var given = _handling.Give(_owner, "sword");

        Assert.NotNull(given);
        Assert.Equal((new Serial(0x40000F00), 1, (Serial?)_backpack.Id), (given.Id, given.Amount, given.ContainerId));
        Assert.Contains(given, _items.GetContents(_backpack.Id));
        Assert.Contains(_fixture.Sender.Sent.OfType<ContainerItemUpdatePacket>(), packet => packet.Item.Serial == given.Id);
    }

    [Fact]
    public void Give_WhatStacks_JoinsTheStackOfItsKindInTheBackpack_AndUsesNoSerial()
    {
        _serials.Serials.Enqueue(new Serial(0x40000F00));

        var given = _handling.Give(_owner, "gold", 30);

        // The 100 coins of the backpack are 130: no new item, no new slot.
        Assert.Same(_gold, given);
        Assert.Equal(130, _gold.Amount);
        Assert.Equal([_gold], _items.GetContents(_backpack.Id));
        Assert.Single(_serials.Serials);
        Assert.Contains(_fixture.Sender.Sent.OfType<ContainerItemUpdatePacket>(), packet => packet.Item.Serial == _gold.Id);
    }

    [Fact]
    public void Give_WhatStacks_JoinsItsStack_EvenWhenTheBackpackHasNoRoomForAnotherItem()
    {
        var handling = Handling(capacity: new StubContainerCapacityService { HasRoomResult = false });

        Assert.Same(_gold, handling.Give(_owner, "gold", 5));
        Assert.Equal(105, _gold.Amount);
    }

    [Theory]
    // Another hue, a prop of its own, or a stack that would pass 60000: a stack apart.
    [InlineData(0x21, false, 100)]
    [InlineData(0, true, 100)]
    [InlineData(0, false, 59_990)]
    public void Give_WhatStacks_MakesAnItemApart_WhenNoStackInTheBackpackCanTakeIt(int hue, bool marked, int amount)
    {
        _serials.Serials.Enqueue(new Serial(0x40000F00));
        _gold.Hue = new Hue((ushort)hue);
        _gold.Amount = amount;

        if (marked)
        {
            _gold.SetProp("blessed.by", "Aria");
        }

        var given = _handling.Give(_owner, "gold", 30);

        Assert.NotNull(given);
        Assert.NotSame(_gold, given);
        Assert.Equal((30, amount), (given.Amount, _gold.Amount));
        Assert.Equal(2, _items.GetContents(_backpack.Id).Count);
    }

    [Fact]
    public void Give_WhatDoesNotStack_IsAnItemApartEachTime()
    {
        _serials.Serials.Enqueue(new Serial(0x40000F00));
        _serials.Serials.Enqueue(new Serial(0x40000F01));

        var first = _handling.Give(_owner, "sword");
        var second = _handling.Give(_owner, "sword");

        Assert.NotSame(first, second);
        Assert.Equal(3, _items.GetContents(_backpack.Id).Count);
    }

    [Fact]
    public void Give_ToAMobileWithoutABackpack_IsNull_AndUsesNoSerial()
    {
        _serials.Serials.Enqueue(new Serial(0x40000F00));
        var bare = new MobileEntity { Id = new Serial(900), Name = "an orc" };

        Assert.Null(_handling.Give(bare, "gold", 30));
        Assert.Single(_serials.Serials);
    }

    // A backpack whose template has a limit of items, reached: a script's gift does not go past it.
    [Fact]
    public void Give_IntoABackpackWithNoRoom_IsNull_AndUsesNoSerial()
    {
        _serials.Serials.Enqueue(new Serial(0x40000F00));
        var handling = Handling(capacity: new StubContainerCapacityService { HasRoomResult = false });

        Assert.Null(handling.Give(_owner, "sword"));
        Assert.Single(_serials.Serials);
        Assert.Single(_items.GetContents(_backpack.Id));
    }

    [Fact]
    public void Give_AnUnknownTemplateOrWithNoSerialLeft_IsNull()
    {
        Assert.Null(_handling.Give(_owner, "sword"));
        _serials.Serials.Enqueue(new Serial(0x40000F00));
        Assert.Null(_handling.Give(_owner, "atlantis", 1));
        Assert.Single(_serials.Serials);
    }

    [Fact]
    public void Consume_TakesUnitsOffThePile_AndShowsTheOwner()
    {
        Assert.True(_handling.Consume(_gold, 40));

        Assert.Equal(60, _gold.Amount);
        Assert.Contains(_fixture.Sender.Sent.OfType<ContainerItemUpdatePacket>(), packet => packet.Item.Serial == _gold.Id);
    }

    [Fact]
    public void Consume_TheWholePile_DeletesIt()
    {
        Assert.True(_handling.Consume(_gold, 100));

        Assert.False(_items.TryGet(_gold.Id, out _));
        Assert.Contains(_fixture.Sender.Sent.OfType<RemoveEntityPacket>(), packet => packet.Serial == _gold.Id);
    }

    [Theory, InlineData(0), InlineData(-1), InlineData(101)]
    public void Consume_AnAmountItDoesNotHave_IsFalse(int amount)
    {
        Assert.False(_handling.Consume(_gold, amount));
        Assert.Equal(100, _gold.Amount);
    }

    [Fact]
    public async Task Consume_AnItemHeldOnACursor_IsFalse()
    {
        Assert.True(_fixture.Sessions.TryGetByCharacterId(_owner.Id, out var session));
        await _fixture.Network.ExecuteOnLoopAsync(() => session.Set(ItemSessionKeys.Held, new HeldItem(_gold.Id)));

        Assert.True(_handling.IsHeld(_gold));
        Assert.False(_handling.Consume(_gold, 10));
        Assert.Equal(100, _gold.Amount);
    }

    [Fact]
    public void Delete_AWornItemOrAContainerThatHoldsItems_IsFalse()
    {
        Assert.False(_handling.Delete(_backpack));
        Assert.True(_items.TryGet(_backpack.Id, out _));
    }

    private ItemHandlingService Handling(IContainerCapacityService? capacity = null)
    {
        return new(
            _items,
            _fixture.Sessions,
            _fixture.Sender,
            _view,
            TestTooltips.Create(_items, _fixture.Mobiles),
            new FakeItemFactoryService(_templates, _tiles),
            _serials,
            capacity: capacity,
            templates: _templates,
            tiles: _tiles
        );
    }

    public async Task DisposeAsync()
    {
        await _fixture.DisposeAsync();
    }
}
