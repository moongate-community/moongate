using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Services.Sessions;
using Moongate.Server.Ultima.Data.Internal.Items;
using Moongate.Server.Ultima.Data.Items;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Handlers.Items;
using Moongate.Server.Ultima.Packets.General;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Types.Items;
using Moongate.Tests.Support.Sessions;
using Moongate.Tests.TestSupport.Packets;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Handlers.Items;

public sealed class LiftRequestPacketHandlerTests : IAsyncDisposable
{
    private static readonly Serial Aria = new(0x00000002);
    private static readonly Serial Bran = new(0x00000003);

    private readonly ItemService _items = new();
    private readonly StubPacketSendService _sender = new();
    private readonly ItemEntity _backpack = Item(0x40000001, 1);
    private readonly ItemEntity _coins = Item(0x40000002, 250);
    private readonly ItemEntity _dagger = Item(0x40000003, 1);
    private readonly ItemEntity _otherBackpack = Item(0x40000004, 1);
    private readonly ItemEntity _otherDagger = Item(0x40000005, 1);

    private SessionFixture _fixture = null!;
    private GameSession _session = null!;

    public LiftRequestPacketHandlerTests()
    {
        _backpack.Equip(Aria, LayerType.Backpack);
        _coins.PutInContainer(_backpack.Id, new Point2D(44, 65));
        _dagger.PutInContainer(_backpack.Id, new Point2D(60, 80));
        _otherBackpack.Equip(Bran, LayerType.Backpack);
        _otherDagger.PutInContainer(_otherBackpack.Id, new Point2D(60, 80));
        _items.Add([_backpack, _coins, _dagger, _otherBackpack, _otherDagger]);
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
    public async Task Handle_PartOfAStack_IsRefused()
    {
        await StartAsync(Aria);

        await LiftAsync(_coins.Id, 10);

        AssertRefused(LiftRejectReasonType.CannotLift, _coins);
        Assert.Null(_session.Get(ItemSessionKeys.Held));
    }

    [Fact]
    public async Task Handle_AnotherCharactersItem_IsRefused()
    {
        await StartAsync(Aria);

        await LiftAsync(_otherDagger.Id, 1);

        AssertRefused(LiftRejectReasonType.CannotLift, _otherDagger);
    }

    [Fact]
    public async Task Handle_TheWornBackpack_IsRefusedWithoutAContainerUpdate()
    {
        await StartAsync(Aria);

        await LiftAsync(_backpack.Id, 1);

        Assert.Equal(LiftRejectReasonType.CannotLift, Assert.IsType<LiftRejectPacket>(Assert.Single(_sender.Sent)).Reason);
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
        _session = new SessionService(_fixture.Loop).GetOrCreate(_fixture.Client);

        if (character is { } id)
        {
            await _fixture.ExecuteOnLoopAsync(() => _session.Set(SessionKeys.CharacterId, id));
        }
    }

    private Task LiftAsync(Serial item, int amount)
    {
        var handler = new LiftRequestPacketHandler(_items, _sender);

        return _fixture.ExecuteOnLoopAsync(() => handler.Handle(_session, new LiftRequestPacket { Item = item, Amount = amount }));
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
