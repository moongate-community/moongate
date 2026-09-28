using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Services.Sessions;
using Moongate.Server.Ultima.Data.Items;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Handlers.Items;
using Moongate.Server.Ultima.Packets.General;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.Support.Sessions;
using Moongate.Tests.TestSupport.Packets;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Handlers.Items;

public sealed class EquipRequestPacketHandlerTests : IAsyncDisposable
{
    private static readonly Serial Aria = new(0x00000002);

    private readonly ItemService _items = new();
    private readonly StubPacketSendService _sender = new();
    private readonly ItemEntity _backpack = new() { Id = new(0x40000001), TemplateId = "backpack", ItemId = 0x0E75 };
    private readonly ItemEntity _dagger = new() { Id = new(0x40000002), TemplateId = "dagger", ItemId = 0x0F52 };

    private SessionFixture _fixture = null!;
    private GameSession _session = null!;

    public EquipRequestPacketHandlerTests()
    {
        _backpack.Equip(Aria, LayerType.Backpack);
        _dagger.PutInContainer(_backpack.Id, new Point2D(60, 80));
        _items.Add([_backpack, _dagger]);
    }

    [Fact]
    public async Task Handle_TheHeldItemDroppedOnThePaperdoll_BouncesBackAndFreesTheHand()
    {
        await StartAsync();
        await _fixture.ExecuteOnLoopAsync(() => _session.Set(ItemSessionKeys.Held, new(_dagger.Id)));

        await EquipAsync();

        Assert.Null(_session.Get(ItemSessionKeys.Held));
        var update = Assert.IsType<ContainerItemUpdatePacket>(Assert.Single(_sender.Sent));
        Assert.Equal((_dagger.Id, _backpack.Id, 60), (update.Item.Serial, update.Item.Container, update.Item.GridX));
        Assert.Equal((_backpack.Id, new Point2D(60, 80)), (_dagger.ContainerId!.Value, _dagger.GridLocation!.Value));
    }

    [Fact]
    public async Task Handle_NothingHeld_SendsNothing()
    {
        await StartAsync();

        await EquipAsync();

        Assert.Empty(_sender.Sent);
    }

    private async Task StartAsync()
    {
        _fixture = await SessionFixture.CreateAsync();
        _session = new SessionService(_fixture.Loop).GetOrCreate(_fixture.Client);
        await _fixture.ExecuteOnLoopAsync(() => _session.Set(SessionKeys.CharacterId, Aria));
    }

    private Task EquipAsync()
    {
        var handler = new EquipRequestPacketHandler(_items, _sender);

        return _fixture.ExecuteOnLoopAsync(() => handler.Handle(_session, new EquipRequestPacket()));
    }

    public async ValueTask DisposeAsync()
    {
        if (_fixture is not null)
        {
            await _fixture.DisposeAsync();
        }
    }
}
