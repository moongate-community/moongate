using System.Buffers.Binary;
using Moongate.Network.Packets.Incoming.Interaction;
using Moongate.Network.Packets.Outgoing.UI;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Spans;

namespace Moongate.Tests.Network.Packets;

public sealed class DyeWindowPacketTests
{
    [Test]
    public void TryParse_WithValidPayload_ShouldPopulateFields()
    {
        var payload = new byte[9];
        payload[0] = 0x95;
        BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(1, 4), 0x40000010u);
        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(5, 2), 0x0FAB);
        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(7, 2), 0x0456);

        var packet = new DyeWindowPacket();
        var ok = packet.TryParse(payload);

        Assert.Multiple(
            () =>
            {
                Assert.That(ok, Is.True);
                Assert.That(packet.TargetSerial, Is.EqualTo(0x40000010u));
                Assert.That(packet.Model, Is.EqualTo(0x0FAB));
                Assert.That(packet.Hue, Is.EqualTo(0x0456));
            }
        );
    }

    [Test]
    public void Write_DisplayDyeWindowPacket_ShouldSerializeExpectedPayload()
    {
        var packet = new DisplayDyeWindowPacket
        {
            TargetSerial = (Moongate.UO.Data.Ids.Serial)0x40000020u,
            Model = 0x0FAB
        };

        var data = Write(packet);

        Assert.Multiple(
            () =>
            {
                Assert.That(data.Length, Is.EqualTo(9));
                Assert.That(data[0], Is.EqualTo(0x95));
                Assert.That(BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(1, 4)), Is.EqualTo(0x40000020u));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(5, 2)), Is.EqualTo(0));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(7, 2)), Is.EqualTo(0x0FAB));
            }
        );
    }

    private static byte[] Write(IGameNetworkPacket packet)
    {
        var writer = new SpanWriter(32, true);
        packet.Write(ref writer);
        var bytes = writer.ToArray();
        writer.Dispose();

        return bytes;
    }
}
