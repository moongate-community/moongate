using System.Net.Sockets;
using Moongate.Network.Client;
using Moongate.Network.Packets.Incoming.House;
using Moongate.Server.Data.Session;
using Moongate.Server.Handlers;
using Moongate.Tests.Server.Support;

namespace Moongate.Tests.Server.Handlers;

public sealed class UpdateViewPublicHouseContentsHandlerTests
{
    [Test]
    public async Task HandlePacketAsync_ShouldConsumeUpdateViewPublicHouseContentsPacket()
    {
        var handler = new UpdateViewPublicHouseContentsHandler(new BasePacketListenerTestOutgoingPacketQueue());
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client));

        var handled = await handler.HandlePacketAsync(session, new UpdateViewPublicHouseContentsPacket());

        Assert.That(handled, Is.True);
    }
}
