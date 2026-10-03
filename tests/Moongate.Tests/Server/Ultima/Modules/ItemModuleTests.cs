using Lua;
using Lua.Standard;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Scripting.Binding;
using Moongate.Scripting.Internal;
using Moongate.Server.Ultima.Data.Internal.Items;
using Moongate.Server.Ultima.Data.Items;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Tests.TestSupport.Ultima.Tiles;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Modules;
using Moongate.Server.Ultima.Packets.General;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Types.Speech;
using Moongate.Tests.TestSupport.Scripting;
using Moongate.Tests.TestSupport.Timing;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Sectors;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Tests.TestSupport.Ultima.Tooltips;
using Moongate.Tests.TestSupport.Ultima.World;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Modules;

public sealed class ItemModuleTests : IAsyncLifetime
{
    private readonly RecordingWorldViewService _view = new();
    private readonly RecordingSpeechService _speech = new();
    private readonly SectorService _sectors = TestSectors.Create();
    private readonly ItemService _items;
    private readonly StubItemSerialPool _serials = new();
    private readonly FakeTileDataService _tiles = new FakeTileDataService()
                                                 .Item(0x0EED, TileFlagType.Generic, 0)
                                                 .Item(0x0E75, TileFlagType.Container, 0)
                                                 .Item(0x0E76, TileFlagType.Container, 0);
    private readonly ItemTemplateService _templates = new(
        new StubDataLoaderService().With(
            new ItemTemplate { Id = "gold", ItemId = new Serial(0x0EED) },
            new ItemTemplate { Id = "sword", ItemId = new Serial(0x0F5E) },
            new ItemTemplate { Id = "bag", ItemId = new Serial(0x0E76) },
            new ItemTemplate { Id = "shirt", ItemId = new Serial(0x1517), Layer = LayerType.Shirt },
            // The graphic does not stack by its tiledata, the template says it does.
            new ItemTemplate { Id = "arrows", ItemId = new Serial(0x0F3F), Stackable = true },
            // And the reverse.
            new ItemTemplate { Id = "relic", ItemId = new Serial(0x0EED), Stackable = false }
        )
    );
    private readonly ItemEntity _backpack = new() { Id = new Serial(0x40000001), TemplateId = "backpack", ItemId = 0x0E75, Amount = 1 };
    private readonly ItemEntity _potions = new() { Id = new Serial(0x40000002), TemplateId = "potion", Name = "a potion", ItemId = 0x0F0E, Amount = 3 };
    private readonly ItemEntity _ground = new() { Id = new Serial(0x40000003), TemplateId = "potion", ItemId = 0x0F0E, Amount = 2 };
    private readonly ItemEntity _sword = new() { Id = new Serial(0x40000004), TemplateId = "sword", ItemId = 0x0F5E, Amount = 1 };

    private readonly SettableClock _clock = new();

    private BroadcastFixture _fixture = null!;
    private MobileEntity _owner = null!;

    public ItemModuleTests()
    {
        _items = TestItems.Create(_sectors);
    }

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        await _fixture.AddAsync(2);
        await _fixture.AddAsync(3);
        Assert.True(_fixture.Mobiles.TryGet(new Serial(2), out _owner!));
        _backpack.Equip(new Serial(2), LayerType.Backpack);
        _potions.PutInContainer(_backpack.Id, new Point2D(44, 65));
        _ground.PlaceOnGround(MapType.Trammel, new Point3D(1600, 1600, 0));
        _sword.Equip(new Serial(2), LayerType.OneHanded);
        _items.Add([_backpack, _potions, _ground, _sword]);
    }

    [Fact]
    public void NameAmountAndOwner_DescribeTheItem()
    {
        var result = Run("return item.name(0x40000002), item.amount(0x40000002), item.owner(0x40000002), item.owner(0x40000003), item.name(0x40000003)");

        Assert.Equal("a potion", result[0].Read<string>());
        Assert.Equal(3, result[1].Read<int>());
        Assert.Equal(2, result[2].Read<int>());
        Assert.Equal(LuaValue.Nil, result[3]);
        Assert.Equal("potion", result[4].Read<string>());
    }

    [Fact]
    public void UnknownSerial_IsNilOrFalse()
    {
        var result = Run("return item.name(12), item.amount(12), item.consume(12), item.delete(12), item.message(12, 2, 'x')");

        Assert.Equal((LuaValue.Nil, LuaValue.Nil), (result[0], result[1]));
        Assert.All(result[2..], value => Assert.False(value.Read<bool>()));
    }

    [Fact]
    public void SetProp_AndGetProp_KeepAValueOnTheItem()
    {
        var result = Run(
            "item.set_prop(0x40000002, 'potion.charges', 2) " +
            "return item.get_prop(0x40000002, 'potion.charges'), item.set_prop(0x40000002, 'bad', {}), item.get_prop(12, 'x')"
        );

        Assert.Equal(2d, result[0].Read<double>());
        Assert.False(result[1].Read<bool>());
        Assert.Equal(LuaValue.Nil, result[2]);
        Assert.Equal(2L, _potions.GetProp<long>("potion.charges"));
        Assert.IsType<long>(_potions.Props!["potion.charges"]);
    }

    [Fact]
    public void ItemId_AndSetItemId_OnTheGround_ChangeTheGraphicAndShowIt()
    {
        var result = Run("local before = item.item_id(0x40000003) return before, item.set_item_id(0x40000003, 0x0676), item.item_id(0x40000003)");

        Assert.Equal((0x0F0E, true, 0x0676), (result[0].Read<int>(), result[1].Read<bool>(), result[2].Read<int>()));
        Assert.Equal(0x0676, _ground.ItemId);
        Assert.Equal(["Appeared 1073741827"], _view.Calls);
    }

    [Fact]
    public void SetLight_OnTheGround_KeepsTheShapeByName_AndNilClearsIt()
    {
        var result = Run("return item.set_light(0x40000003, 'circle150'), item.set_light(0x40000003, 'Circle225'), item.set_light(0x40000003, 'moonbeam')");

        Assert.Equal((true, true, false), (result[0].Read<bool>(), result[1].Read<bool>(), result[2].Read<bool>()));
        Assert.Equal("circle225", _ground.Props!["light"]);
        Assert.Equal(["Appeared 1073741827", "Appeared 1073741827"], _view.Calls);

        Assert.True(Run("return item.set_light(0x40000003)")[0].Read<bool>());
        Assert.Null(_ground.Props?.GetValueOrDefault("light"));
    }

    [Fact]
    public void SetItemId_InAContainer_UpdatesTheOwner()
    {
        Assert.True(Run("return item.set_item_id(0x40000002, 0x0F0C)")[0].Read<bool>());

        Assert.Equal(0x0F0C, _potions.ItemId);
        Assert.Contains(_fixture.Sender.Sent, packet => packet is ContainerItemUpdatePacket);
    }

    [Theory,
     InlineData("return item.set_item_id(0x40000004, 0x0676)"),
     InlineData("return item.set_item_id(0x40000003, -1)"),
     InlineData("return item.set_item_id(0x40000003, 0x10000)"),
     InlineData("return item.set_item_id(12, 0x0676)")]
    public void SetItemId_AWornItemAGraphicOutOfRangeOrUnknown_IsFalse(string chunk)
    {
        Assert.False(Run(chunk)[0].Read<bool>());
        Assert.Equal((0x0F0E, 0x0F5E), (_ground.ItemId, _sword.ItemId));
    }

    [Fact]
    public void Location_OfAGroundItem_AndNilOtherwise()
    {
        var result = Run("local l = item.location(0x40000003) return l.x, l.y, l.z, l.map, item.location(0x40000002)");

        Assert.Equal([1600d, 1600d, 0d, (double)(int)MapType.Trammel], result[..4].Select(value => value.Read<double>()));
        Assert.Equal(LuaValue.Nil, result[4]);
    }

    [Fact]
    public void MoveTo_MovesAGroundItemAndTakesItOffTheOldScreensFirst()
    {
        var result = Run("return item.move_to(0x40000003, 1601, 1599, 5)");

        Assert.True(result[0].Read<bool>());
        Assert.Equal(new Point3D(1601, 1599, 5), _ground.GroundLocation);
        Assert.Equal(["Disappeared 1073741827", "Appeared 1073741827"], _view.Calls);
        Assert.True(_items.IsLyingOnGround(_ground));
    }

    [Theory,
     InlineData("return item.move_to(0x40000002, 1601, 1599, 5)"),
     InlineData("return item.move_to(0x40000004, 1601, 1599, 5)"),
     InlineData("return item.move_to(0x40000003, -1, 1599, 5)"),
     InlineData("return item.move_to(0x40000003, 1601, 1599, 200)"),
     InlineData("return item.move_to(12, 1601, 1599, 5)"),
     InlineData("return item.move_to(0x40000003, 7168, 1599, 5)"),
     InlineData("return item.move_to(0x40000003, 1601, 4096, 5)")]
    public void MoveTo_AnItemNotOnTheGroundOrAnImpossibleSpot_IsFalse(string chunk)
    {
        Assert.False(Run(chunk)[0].Read<bool>());
        Assert.Equal(new Point3D(1600, 1600, 0), _ground.GroundLocation);
        Assert.Empty(_view.Calls);
    }

    [Fact]
    public void PlaySound_OnTheGroundOrCarried_PlaysWhereItIs()
    {
        Assert.True(Run("return item.play_sound(0x40000003, 0xEA)")[0].Read<bool>());
        Assert.True(Run("return item.play_sound(0x40000002, 0x240)")[0].Read<bool>());

        Assert.Equal(
            [(MapType.Trammel, new Point3D(1600, 1600, 0), 0xEA), (MapType.Trammel, _owner.Location, 0x240)],
            _speech.PlacedSounds
        );
    }

    [Theory, InlineData("return item.play_sound(0x40000003, -1)"), InlineData("return item.play_sound(12, 0xEA)")]
    public void PlaySound_ASoundOutOfRangeOrUnknown_IsFalse(string chunk)
    {
        Assert.False(Run(chunk)[0].Read<bool>());
        Assert.Empty(_speech.PlacedSounds);
    }

    [Fact]
    public void Consume_InAContainer_LowersTheAmountAndUpdatesTheOwner()
    {
        var result = Run("return item.consume(0x40000002)");

        Assert.True(result[0].Read<bool>());
        Assert.Equal(2, _potions.Amount);
        Assert.Contains(_fixture.Sender.Sent, packet => packet is ContainerItemUpdatePacket);
        Assert.All(_fixture.Sender.SentSessionIds, id => Assert.Equal(2L, id));
    }

    [Fact]
    public void Consume_TheLastUnits_DeletesTheItemForTheOwnersSave()
    {
        var result = Run("return item.consume(0x40000002, 3)");

        Assert.True(result[0].Read<bool>());
        Assert.False(_items.TryGet(_potions.Id, out _));
        Assert.Contains(_potions.Id, _items.TombstonesOf(new Serial(2)));
        Assert.Contains(_fixture.Sender.Sent, packet => packet is RemoveEntityPacket);
    }

    [Theory, InlineData("item.consume(0x40000002, 4)"), InlineData("item.consume(0x40000002, 0)"), InlineData("item.consume(0x40000004)")]
    public void Consume_TooManyNoneOrWorn_IsFalseAndChangesNothing(string call)
    {
        var result = Run("return " + call);

        Assert.False(result[0].Read<bool>());
        Assert.Equal((3, 1), (_potions.Amount, _sword.Amount));
        Assert.Empty(_fixture.Sender.Sent);
    }

    [Theory, InlineData(0x40000002u), InlineData(0x40000003u)]
    public async Task ConsumeAndDelete_AnItemAPlayerHolds_AreFalseAndShowNothing(uint serial)
    {
        // A lifted item stays where it was taken from until it is dropped: it must not be drawn there again.
        var holder = _fixture.Sessions.GetAll().First(session => session.CharacterId == new Serial(3));
        await _fixture.Network.ExecuteOnLoopAsync(() => holder.Set(ItemSessionKeys.Held, new HeldItem(new Serial(serial))));

        var result = Run($"return item.consume({serial}), item.delete({serial})");

        Assert.All(result, value => Assert.False(value.Read<bool>()));
        Assert.True(_items.TryGet(new Serial(serial), out _));
        Assert.Empty(_fixture.Sender.Sent);
        Assert.Empty(_view.Calls);
    }

    [Fact]
    public void Consume_OnTheGround_ShowsTheNewAmount()
    {
        Run("return item.consume(0x40000003)");

        Assert.Equal(1, _ground.Amount);
        Assert.Equal(["Appeared 1073741827"], _view.Calls);
    }

    [Fact]
    public void Delete_OnTheGround_TakesItOffTheScreens()
    {
        var result = Run("return item.delete(0x40000003)");

        Assert.True(result[0].Read<bool>());
        Assert.False(_items.TryGet(_ground.Id, out _));
        Assert.Equal(["Disappeared 1073741827"], _view.Calls);
    }

    [Theory, InlineData(0x40000004), InlineData(0x40000001)]
    public void Delete_AWornItemOrAContainerWithItems_IsFalse(uint serial)
    {
        var result = Run($"return item.delete({serial})");

        Assert.False(result[0].Read<bool>());
        Assert.True(_items.TryGet(new Serial(serial), out _));
    }

    [Fact]
    public void Message_IsALabelOverTheItemForThatPlayerOnly()
    {
        var result = Run("return item.message(0x40000002, 2, 'You drink the potion.')");

        Assert.True(result[0].Read<bool>());
        Assert.Equal([2L], _fixture.Sender.SentSessionIds);
        var label = Assert.IsType<UnicodeSpeechMessagePacket>(Assert.Single(_fixture.Sender.Sent));
        Assert.Equal((_potions.Id, SpeechType.Label, "You drink the potion."), (label.Serial, label.Type, label.Text));
    }

    [Fact]
    public void MessageCliloc_IsALabelOfTheClientsOwnTextOverTheItem_WithItsArguments()
    {
        var result = Run("return item.message_cliloc(0x40000002, 2, 1042958, '3:05'), item.message_cliloc(0x40000002, 2, 1042955)");

        Assert.Equal([true, true], result.Select(value => value.Read<bool>()));
        Assert.Equal([2L, 2L], _fixture.Sender.SentSessionIds);
        var labels = _fixture.Sender.Sent.Cast<LocalizedMessagePacket>().ToList();
        Assert.Equal((_potions.Id, _potions.ItemId, 1042958, "3:05"), (labels[0].Serial, labels[0].Graphic, labels[0].Cliloc, labels[0].Arguments));
        Assert.Equal((1042955, ""), (labels[1].Cliloc, labels[1].Arguments));
    }

    [Theory]
    [InlineData("item.message_cliloc(0x40000002, 99, 1042955)")]
    [InlineData("item.message_cliloc(0x40000999, 2, 1042955)")]
    [InlineData("item.message_cliloc(0x40000002, 2, 0)")]
    [InlineData("item.message_cliloc(0x40000002, 2, -5)")]
    public void MessageCliloc_NoSuchPlayerOrItemOrText_IsFalse(string call)
    {
        Assert.False(Run("return " + call)[0].Read<bool>());
        Assert.Empty(_fixture.Sender.Sent);
    }

    [Theory, InlineData("item.message(0x40000002, 99, 'hi')"), InlineData("item.message(0x40000002, 2, '  ')")]
    public void Message_NoSuchPlayerOrBlank_IsFalse(string call)
    {
        Assert.False(Run("return " + call)[0].Read<bool>());
        Assert.Empty(_fixture.Sender.Sent);
    }

    public async Task DisposeAsync()
    {
        await _fixture.DisposeAsync();
    }

    [Fact]
    public void Give_PutsANewItemInTheBackpack_AndShowsItToItsOwner()
    {
        _serials.Serials.Enqueue(new Serial(0x40000100));

        var result = Run("return item.give(2, 'gold', 100)");

        Assert.Equal(0x40000100, result[0].Read<long>());
        Assert.True(_items.TryGet(new Serial(0x40000100), out var gold));
        Assert.Equal(("gold", 100, (Serial?)_backpack.Id), (gold.TemplateId, gold.Amount, gold.ContainerId));
        Assert.Equal(gold.Id, Assert.Single(_fixture.Sender.Sent.OfType<ContainerItemUpdatePacket>()).Item.Serial);
        // Not on the slot of the potions already there.
        Assert.NotEqual(_potions.GridIndex, gold.GridIndex);
    }

    [Theory,
     InlineData("return item.give(2, 'nothing')"),
     InlineData("return item.give(999, 'gold')"),
     InlineData("return item.give(3, 'gold')"),
     InlineData("return item.give(2, 'sword', 5)"),
     InlineData("return item.give(2, 'gold', 0)"),
     InlineData("return item.create('nothing', 'Trammel', 1600, 1600, 0)"),
     InlineData("return item.create('gold', 'Trammel', -1, 1600, 0)"),
     InlineData("return item.create('gold', 'Trammel', 1600, 1600, 200)")]
    public void GiveAndCreate_WhatCannotBeMade_IsNil_AndUsesNoSerial(string chunk)
    {
        _serials.Serials.Enqueue(new Serial(0x40000100));

        Assert.Equal(LuaValue.Nil, Run(chunk)[0]);

        Assert.Single(_serials.Serials);
        Assert.False(_items.TryGet(new Serial(0x40000100), out _));
    }

    [Fact]
    public void Give_WithNoSerialLeft_IsNil()
    {
        Assert.Equal(LuaValue.Nil, Run("return item.give(2, 'gold')")[0]);
    }

    [Fact]
    public void Create_PutsANewItemOnTheGround_AndShowsItAround()
    {
        _serials.Serials.Enqueue(new Serial(0x40000100));

        var result = Run("return item.create('gold', 'Trammel', 1500, 1600, 10, 50)");

        Assert.Equal(0x40000100, result[0].Read<long>());
        Assert.True(_items.TryGet(new Serial(0x40000100), out var gold));
        Assert.Equal((50, (Point3D?)new Point3D(1500, 1600, 10), (MapType?)MapType.Trammel), (gold.Amount, gold.GroundLocation, gold.Map));
        Assert.Equal(["Appeared 1073742080"], _view.Calls);
        Assert.Contains(gold, _sectors.GetItemsInRange(MapType.Trammel, new Point3D(1500, 1600, 10), 0));
    }

    [Fact]
    public void TemplateHueAndContainer_DescribeTheItem()
    {
        var result = Run("return item.template(0x40000002), item.hue(0x40000002), item.container(0x40000002), item.container(0x40000003), item.template(12)");

        Assert.Equal("potion", result[0].Read<string>());
        Assert.Equal(0, result[1].Read<int>());
        Assert.Equal(_backpack.Id.Value, result[2].Read<uint>());
        Assert.Equal((LuaValue.Nil, LuaValue.Nil), (result[3], result[4]));
    }

    [Fact]
    public void SetNameAndSetHue_ChangeTheItem_AndShowIt()
    {
        var result = Run("return item.set_name(0x40000003, 'a strange brew'), item.set_hue(0x40000003, 0x26), item.name(0x40000003)");

        Assert.True(result[0].Read<bool>());
        Assert.True(result[1].Read<bool>());
        Assert.Equal("a strange brew", result[2].Read<string>());
        Assert.Equal(("a strange brew", new Hue(0x26)), (_ground.Name, _ground.Hue));
        Assert.Equal(2, _view.Calls.Count(call => call.StartsWith("Appeared", StringComparison.Ordinal)));
    }

    [Fact]
    public void SetName_WithNil_GivesBackTheTemplatesName()
    {
        Run("item.set_name(0x40000002)");

        Assert.Null(_potions.Name);
    }

    [Theory,
     InlineData("return item.set_hue(0x40000004, 5)"),
     InlineData("return item.set_hue(0x40000003, 70000)"),
     InlineData("return item.set_hue(12, 5)"),
     InlineData("return item.set_amount(0x40000004, 2)"),
     InlineData("return item.set_amount(0x40000003, 0)"),
     InlineData("return item.set_amount(0x40000003, 60001)"),
     InlineData("return item.set_amount(0x40000003, 5)"),
     InlineData("return item.set_name(12, 'x')"),
     // Worn.
     InlineData("return item.set_name(0x40000004, 'x')")]
    public void Setters_OnWhatCannotChange_AreFalse(string chunk)
    {
        Assert.False(Run(chunk)[0].Read<bool>());
    }

    [Fact]
    public void SetAmount_OfAStack_SetsIt()
    {
        _serials.Serials.Enqueue(new Serial(0x40000100));

        var result = Run("local gold = item.give(2, 'gold', 10) return item.set_amount(gold, 250), item.amount(gold)");

        Assert.True(result[0].Read<bool>());
        Assert.Equal(250, result[1].Read<int>());
    }

    [Fact]
    public void SetAmount_FollowsTheTemplate_NotOnlyTheGraphic()
    {
        _serials.Serials.Enqueue(new Serial(0x40000100));
        _serials.Serials.Enqueue(new Serial(0x40000101));

        var result = Run(
            "local arrows = item.give(2, 'arrows', 10) local relic = item.give(2, 'relic') " +
            "return item.set_amount(arrows, 50), item.amount(arrows), item.set_amount(relic, 5), item.amount(relic)"
        );

        Assert.True(result[0].Read<bool>());
        Assert.Equal(50, result[1].Read<int>());
        Assert.False(result[2].Read<bool>());
        Assert.Equal(1, result[3].Read<int>());
    }

    [Fact]
    public void MoveInto_OutOfAPlayersBackpackIntoAGroundChest_LeavesItForThatPlayersSave()
    {
        var chest = new ItemEntity { Id = new Serial(0x40000060), TemplateId = "bag", ItemId = 0x0E76, Amount = 1 };
        chest.PlaceOnGround(MapType.Trammel, new Point3D(1601, 1600, 0));
        _items.Add([chest]);

        Assert.True(Run("return item.move_into(0x40000002, 0x40000060)")[0].Read<bool>());

        Assert.Equal((Serial?)chest.Id, _potions.ContainerId);
        Assert.Equal([_potions], _items.TakeReleasedOf(new Serial(2)));
    }

    [Fact]
    public void MoveInto_FromOneMobileToAnother_IsRefused()
    {
        var other = new ItemEntity { Id = new Serial(0x40000070), TemplateId = "backpack", ItemId = 0x0E75, Amount = 1 };
        other.Equip(new Serial(3), LayerType.Backpack);
        _items.Add([other]);

        Assert.False(Run("return item.move_into(0x40000002, 3)")[0].Read<bool>());

        Assert.Equal((Serial?)_backpack.Id, _potions.ContainerId);
    }

    [Fact]
    public void AddLoot_RollsTheTableIntoAContainerOnTheGround_EachItemOnASlotOfItsOwn()
    {
        var chest = GroundChest();
        _serials.Serials.Enqueue(new Serial(0x40000100));
        _serials.Serials.Enqueue(new Serial(0x40000101));

        var result = Run("return item.add_loot(0x40000060, \"two_things\")");

        Assert.Equal(2, result[0].Read<int>());
        var contents = _items.GetContents(chest.Id);
        Assert.Equal(["sword", "sword"], contents.Select(item => item.TemplateId));
        Assert.Equal([new Serial(0x40000100), new Serial(0x40000101)], contents.Select(item => item.Id).Order());
        Assert.Equal(2, contents.Select(item => item.GridIndex).Distinct().Count());
        // Nobody is told: a chest shows its contents when it is opened.
        Assert.Empty(_view.Calls);
        Assert.Empty(_fixture.Sender.Sent);
    }

    [Fact]
    public void AddLoot_WithANumberOfRolls_RollsTheTableThatManyTimes()
    {
        var chest = GroundChest();

        for (var serial = 0x40000100u; serial < 0x40000106u; serial++)
        {
            _serials.Serials.Enqueue(new Serial(serial));
        }

        Assert.Equal(6, Run("return item.add_loot(0x40000060, \"two_things\", 3)")[0].Read<int>());

        var contents = _items.GetContents(chest.Id);
        Assert.Equal(6, contents.Count);
        Assert.Equal(6, contents.Select(item => item.GridIndex).Distinct().Count());
    }

    [Theory, InlineData(0), InlineData(-1)]
    public void AddLoot_WithNoRolls_AddsNothing(int rolls)
    {
        GroundChest();
        _serials.Serials.Enqueue(new Serial(0x40000100));

        Assert.Equal(0, Run($"return item.add_loot(0x40000060, \"two_things\", {rolls})")[0].Read<int>());
    }

    [Fact]
    public void AddLoot_IntoABackpack_ShowsTheItemsToTheOwner()
    {
        _serials.Serials.Enqueue(new Serial(0x40000100));
        _serials.Serials.Enqueue(new Serial(0x40000101));

        Assert.Equal(2, Run("return item.add_loot(2, \"two_things\")")[0].Read<int>());

        Assert.Equal(2, _fixture.Sender.Sent.OfType<ContainerItemUpdatePacket>().Count());
    }

    [Fact]
    public void AddLoot_ARollThatGivesNothing_AddsNothing()
    {
        GroundChest();

        Assert.Equal(0, Run("return item.add_loot(0x40000060, \"nothing\")")[0].Read<int>());
    }

    [Fact]
    public void AddLoot_WithNoSerialLeft_AddsWhatItCan()
    {
        var chest = GroundChest();
        _serials.Serials.Enqueue(new Serial(0x40000100));

        Assert.Equal(1, Run("return item.add_loot(0x40000060, \"two_things\")")[0].Read<int>());

        Assert.Single(_items.GetContents(chest.Id));
    }

    [Theory]
    [InlineData("item.add_loot(0x40000060, \"missing\")")]
    [InlineData("item.add_loot(0x40000004, \"two_things\")")]
    [InlineData("item.add_loot(99, \"two_things\")")]
    [InlineData("item.add_loot(0x40000060, \"\")")]
    public void AddLoot_AnUnknownTableOrSomethingThatIsNoContainer_AddsNothing(string call)
    {
        GroundChest();
        _serials.Serials.Enqueue(new Serial(0x40000100));

        Assert.Equal(0, Run("return " + call)[0].Read<int>());

        Assert.Single(_serials.Serials);
    }

    private ItemEntity GroundChest()
    {
        var chest = new ItemEntity { Id = new Serial(0x40000060), TemplateId = "bag", ItemId = 0x0E76, Amount = 1 };
        chest.PlaceOnGround(MapType.Trammel, new Point3D(1601, 1600, 0));
        _items.Add([chest]);

        return chest;
    }

    [Fact]
    public void Contents_ListsWhatLiesDirectlyInTheContainer()
    {
        var result = Run("local inside = item.contents(0x40000001) return #inside, inside[1], #item.contents(0x40000002), #item.contents(12)");

        Assert.Equal([1, 0x40000002, 0, 0], result.Select(value => value.Read<long>()));
    }

    [Fact]
    public void MoveInto_AGroundItemIntoABackpack_TakesItOffTheGround_AndShowsItToTheOwner()
    {
        var result = Run("return item.move_into(0x40000003, 2)");

        Assert.True(result[0].Read<bool>());
        Assert.Equal((Serial?)_backpack.Id, _ground.ContainerId);
        Assert.Null(_ground.GroundLocation);
        Assert.Equal(["Disappeared 1073741827"], _view.Calls);
        Assert.Equal(_ground.Id, Assert.Single(_fixture.Sender.Sent.OfType<ContainerItemUpdatePacket>()).Item.Serial);
    }

    [Fact]
    public void MoveInto_AContainerItem_MovesIt()
    {
        _serials.Serials.Enqueue(new Serial(0x40000100));

        var result = Run("local bag = item.give(2, 'bag') return item.move_into(0x40000002, bag), item.container(0x40000002) == bag");

        Assert.True(result[0].Read<bool>());
        Assert.True(result[1].Read<bool>());
    }

    [Theory,
     // Worn.
     InlineData("return item.move_into(0x40000004, 2)"),
     // Not a container.
     InlineData("return item.move_into(0x40000003, 0x40000002)"),
     // Into itself.
     InlineData("return item.move_into(0x40000001, 0x40000001)"),
     InlineData("return item.move_into(0x40000003, 999)"),
     InlineData("return item.move_into(12, 2)")]
    public void MoveInto_WhatCannotMove_IsFalse(string chunk)
    {
        Assert.False(Run(chunk)[0].Read<bool>());
    }

    [Fact]
    public void MoveInto_AContainerIntoWhatItHolds_IsFalse()
    {
        _serials.Serials.Enqueue(new Serial(0x40000100));
        _serials.Serials.Enqueue(new Serial(0x40000101));

        var result = Run(
            "local outer = item.give(2, 'bag') local inner = item.give(2, 'bag') item.move_into(inner, outer) return item.move_into(outer, inner)"
        );

        Assert.False(result[0].Read<bool>());
    }

    [Fact]
    public void Equip_AnItemFromTheBackpack_PutsItOnTheMobileAndShowsItToThoseAround()
    {
        var shirt = Shirt(0x40000080);
        shirt.PutInContainer(_backpack.Id, new Point2D(50, 50));
        _items.Add([shirt]);

        Assert.True(Run("return item.equip(0x40000080, 2)")[0].Read<bool>());

        Assert.Equal((new Serial(2), LayerType.Shirt), (shirt.MobileId!.Value, shirt.Layer!.Value));
        Assert.Contains(shirt, _items.GetWorn(new Serial(2)));
        Assert.Equal(["Worn 2 1073741952"], _view.Calls);
    }

    [Fact]
    public void Equip_AGroundItem_TakesItOffTheGround()
    {
        var shirt = Shirt(0x40000080);
        _items.Add([shirt]);
        _items.PlaceOnGround(shirt, MapType.Trammel, new Point3D(1600, 1600, 0));

        Assert.True(Run("return item.equip(0x40000080, 2)")[0].Read<bool>());

        Assert.Null(shirt.GroundLocation);
        Assert.False(_items.IsLyingOnGround(shirt));
        Assert.Equal(["Disappeared 1073741952", "Worn 2 1073741952"], _view.Calls);
    }

    [Fact]
    public void Equip_WhatCannotBeWorn_IsRefused()
    {
        var shirt = Shirt(0x40000080);
        var second = Shirt(0x40000081);
        var others = Shirt(0x40000083);
        var otherBackpack = new ItemEntity { Id = new Serial(0x40000070), TemplateId = "backpack", ItemId = 0x0E75, Amount = 1 };
        otherBackpack.Equip(new Serial(3), LayerType.Backpack);
        shirt.Equip(new Serial(2), LayerType.Shirt);
        second.PutInContainer(_backpack.Id, new Point2D(50, 50));
        others.PutInContainer(otherBackpack.Id, new Point2D(60, 50));
        _items.Add([shirt, second, otherBackpack, others]);

        var result = Run(
            "return item.equip(0x40000081, 2), item.equip(0x40000080, 2), " +
            "item.equip(0x40000002, 2), item.equip(0x40000081, 99), item.equip(12, 2), item.equip(0x40000083, 2)"
        );

        // The layer is taken, it is already worn, potions have no layer, no such mobile, no such item, another mobile
        // carries it.
        Assert.All(result, value => Assert.False(value.Read<bool>()));
        Assert.Empty(_view.Calls);
    }

    [Fact]
    public async Task Equip_AnItemAPlayerHolds_IsRefused()
    {
        var shirt = Shirt(0x40000080);
        shirt.PutInContainer(_backpack.Id, new Point2D(50, 50));
        _items.Add([shirt]);
        var holder = _fixture.Sessions.GetAll().First(session => session.CharacterId == new Serial(2));
        await _fixture.Network.ExecuteOnLoopAsync(() => holder.Set(ItemSessionKeys.Held, new HeldItem(shirt.Id)));

        Assert.False(Run("return item.equip(0x40000080, 2)")[0].Read<bool>());
        Assert.Null(shirt.MobileId);
    }

    [Fact]
    public void Find_GivesTheItemsOfATemplateInAContainerAtAnyDepth_OrCarriedByAMobile()
    {
        var bag = new ItemEntity { Id = new Serial(0x40000090), TemplateId = "bag", ItemId = 0x0E76, Amount = 1 };
        var deep = new ItemEntity { Id = new Serial(0x40000091), TemplateId = "potion", ItemId = 0x0F0E, Amount = 1 };
        bag.PutInContainer(_backpack.Id, new Point2D(50, 50));
        deep.PutInContainer(bag.Id, new Point2D(50, 50));
        _items.Add([bag, deep]);

        var result = Run(
            "local inside, carried, sword = item.find(0x40000001, 'potion'), item.find(2, 'potion'), item.find(2, 'sword') " +
            "table.sort(inside) table.sort(carried) " +
            "return #inside, inside[1], inside[2], #carried, #sword, sword[1], #item.find(0x40000090, 'sword'), " +
            "#item.find(12, 'potion'), #item.find(3, 'potion'), #item.find(0x40000003, 'potion')"
        );

        Assert.Equal([2, 0x40000002, 0x40000091, 2, 1, 0x40000004, 0, 0, 0, 0], result.Select(value => value.Read<int>()));
    }

    [Fact]
    public void StartTimer_KeepsATimerOnTheItem_ThatCanBeReadAndStopped()
    {
        var result = Run(
            "return item.start_timer(0x40000003, 'close', 20), item.timer(0x40000003, 'close'), item.timer(0x40000003, 'open'), " +
            "item.stop_timer(0x40000003, 'close'), item.stop_timer(0x40000003, 'close'), item.timer(0x40000003, 'close')"
        );

        Assert.True(result[0].Read<bool>());
        Assert.Equal(20d, result[1].Read<double>());
        Assert.Equal(LuaValue.Nil, result[2]);
        Assert.Equal((true, false), (result[3].Read<bool>(), result[4].Read<bool>()));
        Assert.Equal(LuaValue.Nil, result[5]);
    }

    [Fact]
    public void StartTimer_KeepsItsDueTimeAsAPropOfTheItem()
    {
        Run("item.start_timer(0x40000003, 'close', 1.5)");

        Assert.Equal(_clock.Now.ToUnixTimeMilliseconds() + 1500, _ground.GetProp<long>("timer.close"));
    }

    [Theory,
     InlineData("item.start_timer(12, 'close', 20)"),
     InlineData("item.start_timer(0x40000003, '', 20)"),
     InlineData("item.start_timer(0x40000003, 'close', 0)"),
     InlineData("item.start_timer(0x40000003, 'close', -3)"),
     InlineData("item.start_timer(0x40000003, 'close', 1/0)"),
     InlineData("item.start_timer(0x40000003, 'close', 1e12)"),
     InlineData("item.stop_timer(12, 'close')")]
    public void TimerFunctions_WithWhatCannotBe_AreFalse(string call)
    {
        Assert.False(Run("return " + call)[0].Read<bool>());
        Assert.Null(_ground.Props);
    }

    private static ItemEntity Shirt(uint serial)
    {
        return new() { Id = new Serial(serial), TemplateId = "shirt", ItemId = 0x1517, Amount = 1 };
    }

    private LuaValue[] Run(string chunk)
    {
        using var state = LuaState.Create();
        state.OpenBasicLibrary();
        state.OpenTableLibrary();
        var factory = new FakeItemFactoryService(_templates, _tiles);
        var module = new ItemModule(
            _items,
            _fixture.Sessions,
            _fixture.Sender,
            _view,
            TestTooltips.Create(_items, _fixture.Mobiles),
            _fixture.Mobiles,
            _speech,
            _sectors,
            factory,
            _serials,
            tiles: _tiles,
            templates: _templates,
            equipment: new EquipmentService(_templates, _tiles, _items),
            timers: new ItemTimerService(
                new RecordingTimerService(),
                new ItemTimerQueue(_clock),
                _items,
                new RecordingItemScriptService(),
                _clock
            ),
            loot: new LootService(
                new StubDataLoaderService().With(
                    // What does not stack comes as that many items.
                    new LootTemplate { Id = "two_things", Entries = [new() { ItemId = "sword", Amount = RangeValueSpec<int>.FromValue(2) }] },
                    new LootTemplate { Id = "nothing", Entries = [new()] }
                ),
                factory,
                _templates,
                _tiles
            )
        );
        new LuaModuleBinder(NoThreadGuard.Instance).Bind(state, module);

        return SyncValueTask.Run(state.DoStringAsync(chunk, "t"));
    }
}
