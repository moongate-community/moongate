using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Network.Interfaces;
using Moongate.Network.Packets.Outgoing;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Data.Session;
using Moongate.Server.Abstractions.Interfaces.World;
using Moongate.Server.Services.Items;
using Moongate.Server.Subscribers;
using Moongate.Tests.Support;

namespace Moongate.Tests.Server.Items;

/// <summary>
/// Lifting part of a stack leaves a remainder the client has never been sent. SplitStack saves it so
/// the refresh subscriber turns that into an add — its own comment says the alternative "reads as the
/// client eating it", which is exactly what a player reported. This checks the wire, not the store.
/// </summary>
public class SplitStackRefreshTests
{
    [Fact]
    public void LiftingPartOfAStack_TellsTheClientAboutTheRemainder()
    {
        var fixture = DragDropFixture.WithGoldInBackpack(100);
        var world = new RecordingWorldService();

        new ItemRefreshSubscriber(fixture.Items, fixture.Persistence, new ContainerOpenerRegistry(), world)
            .Subscribe(fixture.EventBus);

        fixture.Service.Lift(fixture.Actor, fixture.Gold.Id, 40, Serial.Zero, out var heldId, out _);

        var remainder = fixture.BackpackContents().Single(item => item.Id != heldId);

        Assert.Contains(world.Added, sent => sent.Packet.Serial == remainder.Id);
    }

    // Recording the packet is not enough: a send addressed to nobody looks identical to a delivered
    // one from inside the world service. The player who lifted it has to be the recipient.
    [Fact]
    public void LiftingPartOfAStack_TellsTheRightPlayer()
    {
        var fixture = DragDropFixture.WithGoldInBackpack(100);
        var world = new RecordingWorldService();

        new ItemRefreshSubscriber(fixture.Items, fixture.Persistence, new ContainerOpenerRegistry(), world)
            .Subscribe(fixture.EventBus);

        fixture.Service.Lift(fixture.Actor, fixture.Gold.Id, 40, Serial.Zero, out var heldId, out _);

        var remainder = fixture.BackpackContents().Single(item => item.Id != heldId);
        var sent = world.Added.Where(entry => entry.Packet.Serial == remainder.Id).ToList();

        Assert.NotEmpty(sent);
        Assert.All(sent, entry => Assert.Equal(fixture.Actor.Id, entry.Recipient));
    }

    /// <summary>Records the container adds, which is the whole of what this test is about.</summary>
    private sealed class RecordingWorldService : IWorldService
    {
        public List<(Serial Recipient, AddItemToContainerPacket Packet)> Added { get; } = [];

        public int SendToPlayer<TPacket>(Serial mobileId, TPacket packet) where TPacket : IOutgoingPacket
        {
            if (packet is AddItemToContainerPacket add)
            {
                Added.Add((mobileId, add));
            }

            return 1;
        }

        public int Broadcast<TPacket>(TPacket packet) where TPacket : IOutgoingPacket => 0;

        public void SendEnterWorld(PlayerSession session, MobileEntity mobile) { }

        public int SendToPlayersInRange<TPacket>(
            int mapId,
            Point3D center,
            int range,
            TPacket packet,
            Serial? except = null
        )
            where TPacket : IOutgoingPacket => 0;
    }
}
