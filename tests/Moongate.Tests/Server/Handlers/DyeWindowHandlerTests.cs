using System.Net.Sockets;
using Moongate.Network.Client;
using Moongate.Network.Packets.Incoming.Interaction;
using Moongate.Server.Data.Session;
using Moongate.Server.Handlers;
using Moongate.Server.Interfaces.Services.Interaction;
using Moongate.Tests.Server.Support;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Tests.Server.Handlers;

public sealed class DyeWindowHandlerTests
{
    private sealed class DyeWindowHandlerTestService : IDyeColorService
    {
        public DyeWindowPacket? LastPacket { get; private set; }

        public Task<bool> BeginAsync(
            long sessionId,
            Serial dyeTubSerial,
            Func<UOItemEntity, bool>? targetSelectedCallback = null
        )
            => Task.FromResult(false);

        public Task<bool> HandleResponseAsync(GameSession session, DyeWindowPacket packet)
        {
            LastPacket = packet;

            return Task.FromResult(true);
        }

        public Task<bool> SendDyeableAsync(long sessionId, Serial itemSerial, ushort model = 4011)
            => Task.FromResult(false);

        public Task StartAsync()
            => Task.CompletedTask;

        public Task StopAsync()
            => Task.CompletedTask;
    }

    [Test]
    public async Task HandlePacketAsync_ShouldDelegateToDyeColorService()
    {
        var service = new DyeWindowHandlerTestService();
        var handler = new DyeWindowHandler(new BasePacketListenerTestOutgoingPacketQueue(), service);
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client));
        var packet = new DyeWindowPacket { TargetSerial = 0x40000010u, Model = 0x0FAB, Hue = 0x0123 };

        var handled = await handler.HandlePacketAsync(session, packet);

        Assert.Multiple(
            () =>
            {
                Assert.That(handled, Is.True);
                Assert.That(service.LastPacket, Is.SameAs(packet));
            }
        );
    }
}
