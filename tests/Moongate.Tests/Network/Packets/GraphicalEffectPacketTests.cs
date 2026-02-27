using System.Buffers.Binary;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Outgoing.World;
using Moongate.Network.Spans;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Network.Packets;

public class GraphicalEffectPacketTests
{
    [Test]
    public void TryParse_ShouldReadFields()
    {
        var packet = new GraphicalEffectPacket();
        var payload = new byte[]
        {
            0x70,
            0x00,
            0x00, 0x00, 0x00, 0x01,
            0x00, 0x00, 0x00, 0x02,
            0x12, 0x34,
            0x13, 0x89,
            0x0A, 0xBC,
            0xFE,
            0x10, 0x00,
            0x20, 0x00,
            0x7F,
            0x05,
            0x02,
            0x99, 0xAA,
            0x01,
            0x00
        };

        var parsed = packet.TryParse(payload);

        Assert.Multiple(
            () =>
            {
                Assert.That(parsed, Is.True);
                Assert.That(packet.DirectionType, Is.EqualTo(EffectDirectionType.SourceToTarget));
                Assert.That(packet.SourceId, Is.EqualTo((Serial)1u));
                Assert.That(packet.TargetId, Is.EqualTo((Serial)2u));
                Assert.That(packet.ItemId, Is.EqualTo(0x1234));
                Assert.That(packet.SourceLocation, Is.EqualTo(new Point3D(5001, 2748, -2)));
                Assert.That(packet.TargetLocation, Is.EqualTo(new Point3D(4096, 8192, 127)));
                Assert.That(packet.Speed, Is.EqualTo(5));
                Assert.That(packet.Duration, Is.EqualTo(2));
                Assert.That(packet.Unknown2, Is.EqualTo(0x99AA));
                Assert.That(packet.AdjustDirectionDuringAnimation, Is.True);
                Assert.That(packet.ExplodeOnImpact, Is.False);
            }
        );
    }

    [Test]
    public void Write_ShouldSerializeExpectedPayload()
    {
        var packet = new GraphicalEffectPacket(
            EffectDirectionType.FollowCharacter,
            (Serial)0x01020304u,
            (Serial)0xA0B0C0D0u,
            0x3344,
            new Point3D(1200, 1300, 10),
            new Point3D(1400, 1500, -10),
            7,
            9,
            0xBEEF,
            adjustDirectionDuringAnimation: false,
            explodeOnImpact: true
        );

        var data = Write(packet);

        Assert.Multiple(
            () =>
            {
                Assert.That(data.Length, Is.EqualTo(28));
                Assert.That(data[0], Is.EqualTo(0x70));
                Assert.That(data[1], Is.EqualTo((byte)EffectDirectionType.FollowCharacter));
                Assert.That(BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(2, 4)), Is.EqualTo(0x01020304u));
                Assert.That(BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(6, 4)), Is.EqualTo(0xA0B0C0D0u));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(10, 2)), Is.EqualTo(0x3344));
                Assert.That(BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(12, 2)), Is.EqualTo(1200));
                Assert.That(BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(14, 2)), Is.EqualTo(1300));
                Assert.That((sbyte)data[16], Is.EqualTo(10));
                Assert.That(BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(17, 2)), Is.EqualTo(1400));
                Assert.That(BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(19, 2)), Is.EqualTo(1500));
                Assert.That((sbyte)data[21], Is.EqualTo(-10));
                Assert.That(data[22], Is.EqualTo(7));
                Assert.That(data[23], Is.EqualTo(9));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(24, 2)), Is.EqualTo(0xBEEF));
                Assert.That(data[26], Is.EqualTo(0x00));
                Assert.That(data[27], Is.EqualTo(0x01));
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
