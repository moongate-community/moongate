using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Spans;
using Moongate.Server.Data.Packets;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Listener;
using Moongate.Server.Services.Packets;

namespace Moongate.Tests.Server.Services.Packets;

[TestFixture]
public class PacketDispatchServiceTests
{
    private PacketDispatchService _service;

    [SetUp]
    public void SetUp()
    {
        _service = new PacketDispatchService();
    }

    [Test]
    public void NotifyPacketListeners_ShouldCallRegisteredListener()
    {
        var listener = new TestPacketListener();
        _service.AddPacketListener(0x02, listener);

        var packet = new IncomingGamePacket(
            Session: null,
            PacketId: 0x02,
            Packet: new TestGameNetworkPacket(),
            Timestamp: 0
        );

        var result = _service.NotifyPacketListeners(packet);

        Assert.That(result, Is.True);
        Assert.That(listener.CallCount, Is.EqualTo(1));
    }

    [Test]
    public void NotifyPacketListeners_WhenNoListeners_ShouldReturnFalse()
    {
        var packet = new IncomingGamePacket(
            Session: null,
            PacketId: 0x02,
            Packet: new TestGameNetworkPacket(),
            Timestamp: 0
        );

        var result = _service.NotifyPacketListeners(packet);

        Assert.That(result, Is.False);
    }

    [Test]
    public void NotifyPacketListeners_WithMultipleListeners_ShouldCallAll()
    {
        var listener1 = new TestPacketListener();
        var listener2 = new TestPacketListener();
        _service.AddPacketListener(0x02, listener1);
        _service.AddPacketListener(0x02, listener2);

        var packet = new IncomingGamePacket(
            Session: null,
            PacketId: 0x02,
            Packet: new TestGameNetworkPacket(),
            Timestamp: 0
        );

        var result = _service.NotifyPacketListeners(packet);

        Assert.That(result, Is.True);
        Assert.That(listener1.CallCount, Is.EqualTo(1));
        Assert.That(listener2.CallCount, Is.EqualTo(1));
    }

    [Test]
    public void NotifyPacketListeners_WhenListenerThrows_ShouldContinueToNextListener()
    {
        var throwingListener = new ThrowingPacketListener();
        var listener = new TestPacketListener();
        _service.AddPacketListener(0x02, throwingListener);
        _service.AddPacketListener(0x02, listener);

        var packet = new IncomingGamePacket(
            Session: null,
            PacketId: 0x02,
            Packet: new TestGameNetworkPacket(),
            Timestamp: 0
        );

        var result = _service.NotifyPacketListeners(packet);

        Assert.That(result, Is.True);
        Assert.That(listener.CallCount, Is.EqualTo(1));
    }

    private sealed class TestPacketListener : IPacketListener
    {
        public int CallCount { get; private set; }

        public Task<bool> HandlePacketAsync(GameSession session, IGameNetworkPacket packet)
        {
            CallCount++;
            return Task.FromResult(true);
        }
    }

    private sealed class ThrowingPacketListener : IPacketListener
    {
        public Task<bool> HandlePacketAsync(GameSession session, IGameNetworkPacket packet)
        {
            throw new InvalidOperationException("Test exception");
        }
    }

    private sealed class TestGameNetworkPacket : IGameNetworkPacket
    {
        public byte OpCode => 0x02;

        public int Length => 0;

        public bool TryParse(ReadOnlySpan<byte> data)
        {
            return true;
        }

        public void Write(ref SpanWriter writer)
        {
        }
    }
}
