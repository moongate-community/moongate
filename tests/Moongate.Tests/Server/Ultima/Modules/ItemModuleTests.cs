using Lua;
using Lua.Standard;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Scripting.Binding;
using Moongate.Scripting.Internal;
using Moongate.Server.Ultima.Data.Internal.Items;
using Moongate.Server.Ultima.Data.Items;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Modules;
using Moongate.Server.Ultima.Packets.General;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Types.Speech;
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
    private readonly ItemEntity _backpack = new() { Id = new Serial(0x40000001), TemplateId = "backpack", ItemId = 0x0E75, Amount = 1 };
    private readonly ItemEntity _potions = new() { Id = new Serial(0x40000002), TemplateId = "potion", Name = "a potion", ItemId = 0x0F0E, Amount = 3 };
    private readonly ItemEntity _ground = new() { Id = new Serial(0x40000003), TemplateId = "potion", ItemId = 0x0F0E, Amount = 2 };
    private readonly ItemEntity _sword = new() { Id = new Serial(0x40000004), TemplateId = "sword", ItemId = 0x0F5E, Amount = 1 };

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

    private LuaValue[] Run(string chunk)
    {
        using var state = LuaState.Create();
        state.OpenBasicLibrary();
        var module = new ItemModule(_items, _fixture.Sessions, _fixture.Sender, _view, TestTooltips.Create(_items, _fixture.Mobiles), _fixture.Mobiles, _speech, _sectors);
        new LuaModuleBinder(NoThreadGuard.Instance).Bind(state, module);

        return SyncValueTask.Run(state.DoStringAsync(chunk, "t"));
    }
}
