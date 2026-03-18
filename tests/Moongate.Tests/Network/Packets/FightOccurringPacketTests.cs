using System.Buffers.Binary;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Outgoing.Combat;
using Moongate.Network.Spans;
using Moongate.UO.Data.Ids;

namespace Moongate.Tests.Network.Packets;

public sealed class FightOccurringPacketTests
{
    [Test]
    public void Write_ShouldSerializeExpectedPayload()
    {
        var packet = new FightOccurringPacket((Serial)0x00001000u, (Serial)0x00002000u);

        var data = Write(packet);

        Assert.Multiple(
            () =>
            {
                Assert.That(data.Length, Is.EqualTo(10));
                Assert.That(data[0], Is.EqualTo(0x2F));
                Assert.That(data[1], Is.EqualTo(0));
                Assert.That(BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(2, 4)), Is.EqualTo(0x00001000u));
                Assert.That(BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(6, 4)), Is.EqualTo(0x00002000u));
            }
        );
    }

    private static byte[] Write(IGameNetworkPacket packet)
    {
        var writer = new SpanWriter(16, true);
        packet.Write(ref writer);
        var data = writer.ToArray();
        writer.Dispose();

        return data;
    }
}
