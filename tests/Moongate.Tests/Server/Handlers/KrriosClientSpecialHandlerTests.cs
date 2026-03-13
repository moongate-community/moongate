using System.Net.Sockets;
using Moongate.Network.Client;
using Moongate.Network.Packets.Incoming.Movement;
using Moongate.Server.Data.Session;
using Moongate.Server.Handlers;
using Moongate.Tests.Server.Support;

namespace Moongate.Tests.Server.Handlers;

public sealed class KrriosClientSpecialHandlerTests
{
    [Test]
    public async Task HandlePacketAsync_ShouldConsumeKrriosClientSpecialPacket()
    {
        var handler = new KrriosClientSpecialHandler(new BasePacketListenerTestOutgoingPacketQueue());
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client));

        var handled = await handler.HandlePacketAsync(session, new NewMovementRequestPacket());

        Assert.That(handled, Is.True);
    }
}
