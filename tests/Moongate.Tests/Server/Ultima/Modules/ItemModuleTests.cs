using Lua;
using Lua.Standard;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Scripting.Binding;
using Moongate.Scripting.Internal;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Modules;
using Moongate.Server.Ultima.Packets.General;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Types.Speech;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Tests.TestSupport.Ultima.Tooltips;
using Moongate.Tests.TestSupport.Ultima.World;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Modules;

public sealed class ItemModuleTests : IAsyncLifetime
{
    private readonly RecordingWorldViewService _view = new();
    private readonly ItemService _items = TestItems.Create();
    private readonly ItemEntity _backpack = new() { Id = new Serial(0x40000001), TemplateId = "backpack", ItemId = 0x0E75, Amount = 1 };
    private readonly ItemEntity _potions = new() { Id = new Serial(0x40000002), TemplateId = "potion", Name = "a potion", ItemId = 0x0F0E, Amount = 3 };
    private readonly ItemEntity _ground = new() { Id = new Serial(0x40000003), TemplateId = "potion", ItemId = 0x0F0E, Amount = 2 };
    private readonly ItemEntity _sword = new() { Id = new Serial(0x40000004), TemplateId = "sword", ItemId = 0x0F5E, Amount = 1 };

    private BroadcastFixture _fixture = null!;

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        await _fixture.AddAsync(2);
        await _fixture.AddAsync(3);
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
        var module = new ItemModule(_items, _fixture.Sessions, _fixture.Sender, _view, TestTooltips.Create(_items, _fixture.Mobiles));
        new LuaModuleBinder(NoThreadGuard.Instance).Bind(state, module);

        return SyncValueTask.Run(state.DoStringAsync(chunk, "t"));
    }
}
