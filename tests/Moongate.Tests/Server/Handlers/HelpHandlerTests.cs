using System.Net.Sockets;
using Moongate.Network.Client;
using Moongate.Network.Packets.Incoming.UI;
using Moongate.Server.Data.Session;
using Moongate.Server.Handlers;
using Moongate.Server.Interfaces.Services.Interaction;
using Moongate.Tests.Server.Support;

namespace Moongate.Tests.Server.Handlers;

public sealed class HelpHandlerTests
{
    private sealed class HelpHandlerTestHelpRequestService : IHelpRequestService
    {
        public int CallCount { get; private set; }

        public GameSession? LastSession { get; private set; }

        public Task OpenAsync(GameSession session, CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            CallCount++;
            LastSession = session;

            return Task.CompletedTask;
        }

        public Task StartAsync()
            => Task.CompletedTask;

        public Task StopAsync()
            => Task.CompletedTask;
    }

    [Test]
    public async Task HandlePacketAsync_WhenRequestHelpPacketReceived_ShouldDelegateToHelpRequestService()
    {
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var helpRequestService = new HelpHandlerTestHelpRequestService();
        var handler = new HelpHandler(queue, helpRequestService);
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client));
        var packet = new RequestHelpPacket();
        var payload = new byte[258];
        payload[0] = 0x9B;
        Assert.That(packet.TryParse(payload), Is.True);

        var handled = await handler.HandlePacketAsync(session, packet);

        Assert.Multiple(
            () =>
            {
                Assert.That(handled, Is.True);
                Assert.That(helpRequestService.CallCount, Is.EqualTo(1));
                Assert.That(helpRequestService.LastSession, Is.SameAs(session));
            }
        );
    }
}
