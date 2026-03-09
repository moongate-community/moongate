using System.Buffers.Binary;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Outgoing.Entity;
using Moongate.Network.Spans;
using Moongate.UO.Data.Ids;

namespace Moongate.Tests.Network.Packets;

public sealed class MobileAnimationPacketTests
{
    [Test]
    public void Write_ShouldSerializeExpectedPayload()
    {
        var packet = new MobileAnimationPacket(
            (Serial)0x40000001u,
            action: 17,
            frameCount: 7,
            repeatCount: 1,
            forward: true,
            repeat: false,
            delay: 3
        );

        var data = Write(packet);

        Assert.Multiple(
            () =>
            {
                Assert.That(data.Length, Is.EqualTo(14));
                Assert.That(data[0], Is.EqualTo(0x6E));
                Assert.That(BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(1, 4)), Is.EqualTo(0x40000001u));
                Assert.That(BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(5, 2)), Is.EqualTo(17));
                Assert.That(BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(7, 2)), Is.EqualTo(7));
                Assert.That(BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(9, 2)), Is.EqualTo(1));
                Assert.That(data[11], Is.EqualTo(0)); // reverse=false when forward=true
                Assert.That(data[12], Is.EqualTo(0)); // repeat=false
                Assert.That(data[13], Is.EqualTo(3));
            }
        );
    }

    private static byte[] Write(IGameNetworkPacket packet)
    {
        var writer = new SpanWriter(32, true);
        packet.Write(ref writer);
        var data = writer.ToArray();
        writer.Dispose();

        return data;
    }
}
