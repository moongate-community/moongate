using System.Net.Sockets;
using Moongate.Network.Client;
using Moongate.Network.Packets.Incoming.Player;
using Moongate.Server.Data.Session;
using Moongate.Server.Handlers;
using Moongate.Tests.Server.Support;

namespace Moongate.Tests.Server.Handlers;

public class ClientViewRangeHandlerTests
{
    [Test]
    public async Task HandlePacketAsync_ShouldClampAndEcho_WhenRangeIsBelowMinimum()
    {
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var handler = new ClientViewRangeHandler(queue);
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client));

        var handled = await handler.HandlePacketAsync(session, new ClientViewRangePacket(2));
        var dequeued = queue.TryDequeue(out var outbound);

        Assert.Multiple(
            () =>
            {
                Assert.That(handled, Is.True);
                Assert.That(session.ViewRange, Is.EqualTo(5));
                Assert.That(dequeued, Is.True);
                Assert.That(outbound.Packet, Is.TypeOf<ClientViewRangePacket>());
                Assert.That(((ClientViewRangePacket)outbound.Packet).Range, Is.EqualTo(5));
            }
        );
    }

    [Test]
    public async Task HandlePacketAsync_ShouldClampAndEcho_WhenRangeIsAboveMaximum()
    {
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var handler = new ClientViewRangeHandler(queue);
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client));

        var handled = await handler.HandlePacketAsync(session, new ClientViewRangePacket(30));
        var dequeued = queue.TryDequeue(out var outbound);

        Assert.Multiple(
            () =>
            {
                Assert.That(handled, Is.True);
                Assert.That(session.ViewRange, Is.EqualTo(18));
                Assert.That(dequeued, Is.True);
                Assert.That(outbound.Packet, Is.TypeOf<ClientViewRangePacket>());
                Assert.That(((ClientViewRangePacket)outbound.Packet).Range, Is.EqualTo(18));
            }
        );
    }
}
