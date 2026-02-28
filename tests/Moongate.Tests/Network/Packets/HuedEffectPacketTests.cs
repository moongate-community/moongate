using System.Buffers.Binary;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Outgoing.World;
using Moongate.Network.Spans;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Network.Packets;

public class HuedEffectPacketTests
{
    [Test]
    public void TryParse_ShouldReadFields()
    {
        var packet = new HuedEffectPacket();
        var payload = new byte[]
        {
            0xC0,
            0x02,
            0x00, 0x00, 0x00, 0x11,
            0x00, 0x00, 0x00, 0x22,
            0x12, 0x34,
            0x04, 0x56,
            0x07, 0x89,
            0xFA,
            0x0A, 0xBC,
            0x0D, 0xEF,
            0x0F,
            0x04,
            0x02,
            0xAA,
            0xBB,
            0x01,
            0x00,
            0x11, 0x22, 0x33, 0x44,
            0x55, 0x66, 0x77, 0x88
        };

        var parsed = packet.TryParse(payload);

        Assert.Multiple(
            () =>
            {
                Assert.That(parsed, Is.True);
                Assert.That(packet.DirectionType, Is.EqualTo(EffectDirectionType.StayAtLocation));
                Assert.That(packet.SourceId, Is.EqualTo((Serial)0x11u));
                Assert.That(packet.TargetId, Is.EqualTo((Serial)0x22u));
                Assert.That(packet.ItemId, Is.EqualTo(0x1234));
                Assert.That(packet.SourceLocation, Is.EqualTo(new Point3D(1110, 1929, -6)));
                Assert.That(packet.TargetLocation, Is.EqualTo(new Point3D(2748, 3567, 15)));
                Assert.That(packet.Speed, Is.EqualTo(4));
                Assert.That(packet.Duration, Is.EqualTo(2));
                Assert.That(packet.Unknown1, Is.EqualTo(0xAA));
                Assert.That(packet.Unknown2, Is.EqualTo(0xBB));
                Assert.That(packet.FixedDirection, Is.True);
                Assert.That(packet.Explode, Is.False);
                Assert.That(packet.Hue, Is.EqualTo(0x11223344));
                Assert.That(packet.RenderMode, Is.EqualTo(0x55667788));
            }
        );
    }

    [Test]
    public void Write_ShouldSerializeExpectedPayload()
    {
        var packet = new HuedEffectPacket(
            EffectDirectionType.LightningStrike,
            (Serial)0x01010101u,
            (Serial)0x02020202u,
            0x7777,
            new Point3D(100, 200, -30),
            new Point3D(300, 400, 30),
            speed: 3,
            duration: 8,
            fixedDirection: false,
            explode: true,
            hue: 0x12345678,
            renderMode: 0x7ABCDEF0
        );

        var data = Write(packet);

        Assert.Multiple(
            () =>
            {
                Assert.That(data.Length, Is.EqualTo(36));
                Assert.That(data[0], Is.EqualTo(0xC0));
                Assert.That(data[1], Is.EqualTo((byte)EffectDirectionType.LightningStrike));
                Assert.That(BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(2, 4)), Is.EqualTo(0x01010101u));
                Assert.That(BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(6, 4)), Is.EqualTo(0x02020202u));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(10, 2)), Is.EqualTo(0x7777));
                Assert.That(BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(12, 2)), Is.EqualTo(100));
                Assert.That(BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(14, 2)), Is.EqualTo(200));
                Assert.That((sbyte)data[16], Is.EqualTo(-30));
                Assert.That(BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(17, 2)), Is.EqualTo(300));
                Assert.That(BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(19, 2)), Is.EqualTo(400));
                Assert.That((sbyte)data[21], Is.EqualTo(30));
                Assert.That(data[22], Is.EqualTo(3));
                Assert.That(data[23], Is.EqualTo(8));
                Assert.That(data[24], Is.EqualTo(0x00));
                Assert.That(data[25], Is.EqualTo(0x00));
                Assert.That(data[26], Is.EqualTo(0x00));
                Assert.That(data[27], Is.EqualTo(0x01));
                Assert.That(BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(28, 4)), Is.EqualTo(0x12345678));
                Assert.That(BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(32, 4)), Is.EqualTo(0x7ABCDEF0));
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
