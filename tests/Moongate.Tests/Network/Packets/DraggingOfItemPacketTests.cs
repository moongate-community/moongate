using System.Buffers.Binary;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Outgoing.Entity;
using Moongate.Network.Spans;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;

namespace Moongate.Tests.Network.Packets;

public class DraggingOfItemPacketTests
{
    [Test]
    public void Write_ShouldSerializeDraggingOfItemPacket()
    {
        var packet = new DraggingOfItemPacket(
            itemId: 0x0EED,
            hue: 0x0455,
            stackCount: 0x0010,
            sourceId: (Serial)0x40000010u,
            sourceLocation: new Point3D(100, 200, 7),
            targetId: (Serial)0x40000020u,
            targetLocation: new Point3D(110, 210, 10)
        );

        var data = Write(packet);

        Assert.Multiple(
            () =>
            {
                Assert.That(data.Length, Is.EqualTo(26));
                Assert.That(data[0], Is.EqualTo(0x23));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(1, 2)), Is.EqualTo(0x0EED));
                Assert.That(data[3], Is.EqualTo(0x00));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(4, 2)), Is.EqualTo(0x0455));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(6, 2)), Is.EqualTo(0x0010));
                Assert.That(BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(8, 4)), Is.EqualTo(0x40000010u));
                Assert.That(BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(12, 2)), Is.EqualTo(100));
                Assert.That(BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(14, 2)), Is.EqualTo(200));
                Assert.That(unchecked((sbyte)data[16]), Is.EqualTo(7));
                Assert.That(BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(17, 4)), Is.EqualTo(0x40000020u));
                Assert.That(BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(21, 2)), Is.EqualTo(110));
                Assert.That(BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(23, 2)), Is.EqualTo(210));
                Assert.That(unchecked((sbyte)data[25]), Is.EqualTo(10));
            }
        );
    }

    [Test]
    public void TryParse_ShouldReadDraggingOfItemPacketFields()
    {
        var packet = new DraggingOfItemPacket();
        var payload = new byte[]
        {
            0x23,
            0x0E, 0xED,
            0x00,
            0x04, 0x55,
            0x00, 0x10,
            0x40, 0x00, 0x00, 0x10,
            0x00, 0x64,
            0x00, 0xC8,
            0x07,
            0x40, 0x00, 0x00, 0x20,
            0x00, 0x6E,
            0x00, 0xD2,
            0x0A
        };

        var parsed = packet.TryParse(payload);

        Assert.Multiple(
            () =>
            {
                Assert.That(parsed, Is.True);
                Assert.That(packet.ItemId, Is.EqualTo(0x0EED));
                Assert.That(packet.Unknown, Is.EqualTo(0x00));
                Assert.That(packet.Hue, Is.EqualTo(0x0455));
                Assert.That(packet.StackCount, Is.EqualTo(0x0010));
                Assert.That(packet.SourceId, Is.EqualTo((Serial)0x40000010u));
                Assert.That(packet.SourceLocation, Is.EqualTo(new Point3D(100, 200, 7)));
                Assert.That(packet.TargetId, Is.EqualTo((Serial)0x40000020u));
                Assert.That(packet.TargetLocation, Is.EqualTo(new Point3D(110, 210, 10)));
            }
        );
    }

    private static byte[] Write(IGameNetworkPacket packet)
    {
        var writer = new SpanWriter(64, true);
        packet.Write(ref writer);
        var data = writer.ToArray();
        writer.Dispose();

        return data;
    }
}
