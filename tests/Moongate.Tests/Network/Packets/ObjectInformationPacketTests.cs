using System.Buffers.Binary;
using Moongate.Network.Packets.Outgoing.Entity;
using Moongate.Network.Spans;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Network.Packets;

public class ObjectInformationPacketTests
{
    [Test]
    public void Write_ForMulti_ShouldSerializeMultiLayout()
    {
        var packet = ObjectInformationPacket.ForMulti(
            (Serial)0x40000100u,
            0x1F00,
            new(10, 20, 5)
        );

        var data = Write(packet);

        Assert.Multiple(
            () =>
            {
                Assert.That(data.Length, Is.EqualTo(24));
                Assert.That(data[0], Is.EqualTo(0xF3));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(1, 2)), Is.EqualTo(0x0001));
                Assert.That(data[3], Is.EqualTo(0x02));
                Assert.That(BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(4, 4)), Is.EqualTo(0x40000100u));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(8, 2)), Is.EqualTo(0x1F00));
                Assert.That(data[10], Is.EqualTo(0x00));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(11, 2)), Is.EqualTo(0x0001));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(13, 2)), Is.EqualTo(0x0001));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(15, 2)), Is.EqualTo(10));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(17, 2)), Is.EqualTo(20));
                Assert.That(unchecked((sbyte)data[19]), Is.EqualTo(5));
                Assert.That(data[20], Is.EqualTo(0x00));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(21, 2)), Is.EqualTo(0x0000));
                Assert.That(data[23], Is.EqualTo(0x00));
            }
        );
    }

    [Test]
    public void Write_WithItem_ShouldSerializeObjectInformationPacket()
    {
        var item = new UOItemEntity
        {
            Id = (Serial)0x40000033u,
            ItemId = 0x0EED,
            Amount = 0x14,
            Location = new(1234, 2345, 10),
            Hue = 0x0456,
            Direction = DirectionType.SouthWest
        };

        var packet = new ObjectInformationPacket(item, layer: 0x01, flags: ObjectInfoFlags.Hidden | ObjectInfoFlags.Movable);

        var data = Write(packet);

        Assert.Multiple(
            () =>
            {
                Assert.That(data.Length, Is.EqualTo(24));
                Assert.That(data[0], Is.EqualTo(0xF3));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(1, 2)), Is.EqualTo(0x0001));
                Assert.That(data[3], Is.EqualTo(0x00));
                Assert.That(BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(4, 4)), Is.EqualTo(0x40000033u));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(8, 2)), Is.EqualTo(0x0EED));
                Assert.That(data[10], Is.EqualTo((byte)DirectionType.SouthWest));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(11, 2)), Is.EqualTo(0x0014));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(13, 2)), Is.EqualTo(0x0014));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(15, 2)), Is.EqualTo(1234));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(17, 2)), Is.EqualTo(2345));
                Assert.That(unchecked((sbyte)data[19]), Is.EqualTo(10));
                Assert.That(data[20], Is.EqualTo(0x01));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(21, 2)), Is.EqualTo(0x0456));
                Assert.That(data[23], Is.EqualTo((byte)(ObjectInfoFlags.Hidden | ObjectInfoFlags.Movable)));
            }
        );
    }

    private static byte[] Write(ObjectInformationPacket packet)
    {
        var writer = new SpanWriter(24, true);
        packet.Write(ref writer);
        var data = writer.ToArray();
        writer.Dispose();

        return data;
    }
}
