using System.Buffers.Binary;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Outgoing.World;
using Moongate.Network.Spans;
using Moongate.UO.Data.Geometry;

namespace Moongate.Tests.Network.Packets;

public sealed class ServerChangePacketTests
{
    [Test]
    public void TryParse_ShouldReadFields()
    {
        var packet = new ServerChangePacket();
        var payload = new byte[]
        {
            0x76,
            0x01, 0x02,
            0x03, 0x04,
            0xFF, 0xFE,
            0x09,
            0x0A, 0x0B,
            0x0C, 0x0D,
            0x0E, 0x0F,
            0x10, 0x11
        };

        var parsed = packet.TryParse(payload);

        Assert.Multiple(
            () =>
            {
                Assert.That(parsed, Is.True);
                Assert.That(packet.Location, Is.EqualTo(new Point3D(0x0102, 0x0304, -2)));
                Assert.That(packet.Unknown0, Is.EqualTo(0x09));
                Assert.That(packet.Unknown1, Is.EqualTo(0x0A0B));
                Assert.That(packet.Unknown2, Is.EqualTo(0x0C0D));
                Assert.That(packet.MapWidth, Is.EqualTo(0x0E0F));
                Assert.That(packet.MapHeight, Is.EqualTo(0x1011));
            }
        );
    }

    [Test]
    public void Write_ShouldSerializeExpectedPayload()
    {
        var packet = new ServerChangePacket(
            new Point3D(3613, 2585, 0),
            7168,
            4096
        );

        var data = Write(packet);

        Assert.Multiple(
            () =>
            {
                Assert.That(data.Length, Is.EqualTo(16));
                Assert.That(data[0], Is.EqualTo(0x76));
                Assert.That(BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(1, 2)), Is.EqualTo(3613));
                Assert.That(BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(3, 2)), Is.EqualTo(2585));
                Assert.That(BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(5, 2)), Is.EqualTo(0));
                Assert.That(data[7], Is.EqualTo(0));
                Assert.That(BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(8, 2)), Is.EqualTo(0));
                Assert.That(BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(10, 2)), Is.EqualTo(0));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(12, 2)), Is.EqualTo(7168));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(14, 2)), Is.EqualTo(4096));
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
