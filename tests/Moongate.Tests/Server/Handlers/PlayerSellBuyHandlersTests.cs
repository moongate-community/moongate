using System.Net.Sockets;
using Moongate.Network.Client;
using Moongate.Network.Packets.Incoming.Trading;
using Moongate.Network.Spans;
using Moongate.Server.Data.Session;
using Moongate.Server.Handlers;
using Moongate.Server.Interfaces.Services.Interaction;
using Moongate.Tests.Server.Support;
using Moongate.UO.Data.Ids;

namespace Moongate.Tests.Server.Handlers;

public sealed class PlayerSellBuyHandlersTests
{
    private sealed class RecordingPlayerSellBuyService : IPlayerSellBuyService
    {
        public BuyItemsPacket? LastBuyPacket { get; private set; }
        public int LastBuyRequestCallCount { get; private set; }
        public Serial LastBuyRequestVendor { get; private set; }
        public long LastBuySessionId { get; private set; }
        public SellListReplyPacket? LastSellPacket { get; private set; }
        public int LastSellRequestCallCount { get; private set; }
        public Serial LastSellRequestVendor { get; private set; }

        public Task HandleBuyItemsAsync(long sessionId, BuyItemsPacket packet, CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            LastBuySessionId = sessionId;
            LastBuyPacket = packet;

            return Task.CompletedTask;
        }

        public Task HandleSellListReplyAsync(
            long sessionId,
            SellListReplyPacket packet,
            CancellationToken cancellationToken = default
        )
        {
            _ = cancellationToken;
            LastBuySessionId = sessionId;
            LastSellPacket = packet;

            return Task.CompletedTask;
        }

        public Task HandleVendorBuyRequestAsync(
            long sessionId,
            Serial vendorSerial,
            CancellationToken cancellationToken = default
        )
        {
            _ = cancellationToken;
            LastBuySessionId = sessionId;
            LastBuyRequestVendor = vendorSerial;
            LastBuyRequestCallCount++;

            return Task.CompletedTask;
        }

        public Task HandleVendorSellRequestAsync(
            long sessionId,
            Serial vendorSerial,
            CancellationToken cancellationToken = default
        )
        {
            _ = cancellationToken;
            LastBuySessionId = sessionId;
            LastSellRequestVendor = vendorSerial;
            LastSellRequestCallCount++;

            return Task.CompletedTask;
        }
    }

    [Test]
    public async Task BuyItemsHandler_ShouldDelegateToPlayerSellBuyService()
    {
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var service = new RecordingPlayerSellBuyService();
        var handler = new BuyItemsHandler(queue, service);
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client));
        var packet = BuildBuyItemsPacket();

        var handled = await handler.HandlePacketAsync(session, packet);

        Assert.Multiple(
            () =>
            {
                Assert.That(handled, Is.True);
                Assert.That(service.LastBuyPacket, Is.SameAs(packet));
                Assert.That(service.LastBuySessionId, Is.EqualTo(session.SessionId));
            }
        );
    }

    [Test]
    public async Task SellListReplyHandler_ShouldDelegateToPlayerSellBuyService()
    {
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var service = new RecordingPlayerSellBuyService();
        var handler = new SellListReplyHandler(queue, service);
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client));
        var packet = BuildSellListReplyPacket();

        var handled = await handler.HandlePacketAsync(session, packet);

        Assert.Multiple(
            () =>
            {
                Assert.That(handled, Is.True);
                Assert.That(service.LastSellPacket, Is.SameAs(packet));
                Assert.That(service.LastBuySessionId, Is.EqualTo(session.SessionId));
            }
        );
    }

    [Test]
    public async Task VendorBuyRequestHandler_ShouldDelegateToPlayerSellBuyService()
    {
        var service = new RecordingPlayerSellBuyService();
        var handler = new VendorBuyRequestHandler(service);

        await handler.HandleAsync(new(42, (Serial)0x00001000u));

        Assert.Multiple(
            () =>
            {
                Assert.That(service.LastBuyRequestCallCount, Is.EqualTo(1));
                Assert.That(service.LastBuySessionId, Is.EqualTo(42));
                Assert.That(service.LastBuyRequestVendor, Is.EqualTo((Serial)0x00001000u));
            }
        );
    }

    [Test]
    public async Task VendorSellRequestHandler_ShouldDelegateToPlayerSellBuyService()
    {
        var service = new RecordingPlayerSellBuyService();
        var handler = new VendorSellRequestHandler(service);

        await handler.HandleAsync(new(77, (Serial)0x00002000u));

        Assert.Multiple(
            () =>
            {
                Assert.That(service.LastSellRequestCallCount, Is.EqualTo(1));
                Assert.That(service.LastBuySessionId, Is.EqualTo(77));
                Assert.That(service.LastSellRequestVendor, Is.EqualTo((Serial)0x00002000u));
            }
        );
    }

    private static BuyItemsPacket BuildBuyItemsPacket()
    {
        var writer = new SpanWriter(64, true);
        writer.Write((byte)0x3B);
        writer.Write((ushort)0);
        writer.Write(0x00001000u);
        writer.Write((byte)0x02);
        writer.Write((byte)0x1A);
        writer.Write(0x40000001u);
        writer.Write((short)1);
        writer.WritePacketLength();
        var bytes = writer.ToArray();
        writer.Dispose();

        var packet = new BuyItemsPacket();
        Assert.That(packet.TryParse(bytes), Is.True);

        return packet;
    }

    private static SellListReplyPacket BuildSellListReplyPacket()
    {
        var writer = new SpanWriter(64, true);
        writer.Write((byte)0x9F);
        writer.Write((ushort)0);
        writer.Write(0x00001000u);
        writer.Write((ushort)1);
        writer.Write(0x40000002u);
        writer.Write((short)1);
        writer.WritePacketLength();
        var bytes = writer.ToArray();
        writer.Dispose();

        var packet = new SellListReplyPacket();
        Assert.That(packet.TryParse(bytes), Is.True);

        return packet;
    }
}
