using System.Net.Sockets;
using Moongate.Network.Client;
using Moongate.Network.Packets.Incoming.Interaction;
using Moongate.Server.Data.Session;
using Moongate.Server.Handlers;
using Moongate.Server.Interfaces.Services.Interaction;
using Moongate.Tests.Server.Support;
using Moongate.UO.Data.Ids;

namespace Moongate.Tests.Server.Handlers;

public sealed class RequestAttackHandlerTests
{
    private sealed class RecordingCombatService : ICombatService
    {
        public Serial LastAttackerId { get; private set; }
        public Serial LastDefenderId { get; private set; }
        public int TrySetCombatantCallCount { get; private set; }

        public Task<bool> ClearCombatantAsync(Serial attackerId, CancellationToken cancellationToken = default)
        {
            _ = attackerId;
            _ = cancellationToken;

            return Task.FromResult(true);
        }

        public Task<bool> TrySetCombatantAsync(
            Serial attackerId,
            Serial defenderId,
            CancellationToken cancellationToken = default
        )
        {
            _ = cancellationToken;
            LastAttackerId = attackerId;
            LastDefenderId = defenderId;
            TrySetCombatantCallCount++;

            return Task.FromResult(true);
        }
    }

    [Test]
    public async Task HandlePacketAsync_ShouldDelegateToCombatService()
    {
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var service = new RecordingCombatService();
        var handler = new RequestAttackHandler(queue, service);
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client))
        {
            CharacterId = (Serial)0x00000002u
        };
        var packet = new RequestAttackPacket();
        Assert.That(packet.TryParse(new byte[] { 0x05, 0x00, 0x00, 0x10, 0x00 }), Is.True);

        var handled = await handler.HandlePacketAsync(session, packet);

        Assert.Multiple(
            () =>
            {
                Assert.That(handled, Is.True);
                Assert.That(service.TrySetCombatantCallCount, Is.EqualTo(1));
                Assert.That(service.LastAttackerId, Is.EqualTo((Serial)0x00000002u));
                Assert.That(service.LastDefenderId, Is.EqualTo((Serial)0x00001000u));
            }
        );
    }
}
