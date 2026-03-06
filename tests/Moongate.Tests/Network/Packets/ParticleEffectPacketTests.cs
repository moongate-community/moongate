using System.Buffers.Binary;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Outgoing.World;
using Moongate.Network.Spans;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Network.Packets;

public class ParticleEffectPacketTests
{
    [Test]
    public void TryParse_ShouldReadFields()
    {
        var packet = new ParticleEffectPacket();
        var payload = new byte[]
        {
            0xC7,
            0x00,
            0x00, 0x00, 0x00, 0x05,
            0x00, 0x00, 0x00, 0x06,
            0x09, 0x09,
            0x01, 0xF4,
            0x02, 0x58,
            0x01,
            0x02, 0xBC,
            0x03, 0x20,
            0xF0,
            0x04,
            0x0A,
            0x00,
            0x01,
            0x00,
            0x01,
            0x01, 0x23, 0x45, 0x67,
            0x11, 0x22, 0x33, 0x44,
            0x07, 0x89,
            0x0A, 0xBC,
            0x0D, 0xEF,
            0x00, 0x00, 0x00, 0x09,
            0x1E,
            0xBE, 0xEF
        };

        var parsed = packet.TryParse(payload);

        Assert.Multiple(
            () =>
            {
                Assert.That(parsed, Is.True);
                Assert.That(packet.DirectionType, Is.EqualTo(EffectDirectionType.SourceToTarget));
                Assert.That(packet.SourceId, Is.EqualTo((Serial)0x05u));
                Assert.That(packet.TargetId, Is.EqualTo((Serial)0x06u));
                Assert.That(packet.ItemId, Is.EqualTo(0x0909));
                Assert.That(packet.SourceLocation, Is.EqualTo(new Point3D(500, 600, 1)));
                Assert.That(packet.TargetLocation, Is.EqualTo(new Point3D(700, 800, -16)));
                Assert.That(packet.Speed, Is.EqualTo(4));
                Assert.That(packet.Duration, Is.EqualTo(10));
                Assert.That(packet.Unknown1, Is.EqualTo(0x00));
                Assert.That(packet.Unknown2, Is.EqualTo(0x01));
                Assert.That(packet.FixedDirection, Is.False);
                Assert.That(packet.Explode, Is.True);
                Assert.That(packet.Hue, Is.EqualTo(0x01234567));
                Assert.That(packet.RenderMode, Is.EqualTo(0x11223344));
                Assert.That(packet.Effect, Is.EqualTo(0x0789));
                Assert.That(packet.ExplodeEffect, Is.EqualTo(0x0ABC));
                Assert.That(packet.ExplodeSound, Is.EqualTo(0x0DEF));
                Assert.That(packet.EffectSerial, Is.EqualTo((Serial)0x09u));
                Assert.That(packet.Layer, Is.EqualTo(0x1E));
                Assert.That(packet.Unknown3, Is.EqualTo(0xBEEF));
            }
        );
    }

    [Test]
    public void Write_ShouldSerializeExpectedPayload()
    {
        var packet = new ParticleEffectPacket(
            EffectDirectionType.FollowCharacter,
            (Serial)0x11111111u,
            (Serial)0x22222222u,
            0x4444,
            new(1000, 1001, 5),
            new(2000, 2001, -5),
            2,
            3,
            true,
            false,
            0x01020304,
            0x11121314,
            0xCCCC,
            0xDDDD,
            0xEEEE,
            (Serial)0x33333333u,
            0x04,
            0x9999
        );

        var data = Write(packet);

        Assert.Multiple(
            () =>
            {
                Assert.That(data.Length, Is.EqualTo(49));
                Assert.That(data[0], Is.EqualTo(0xC7));
                Assert.That(data[1], Is.EqualTo((byte)EffectDirectionType.FollowCharacter));
                Assert.That(BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(2, 4)), Is.EqualTo(0x11111111u));
                Assert.That(BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(6, 4)), Is.EqualTo(0x22222222u));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(10, 2)), Is.EqualTo(0x4444));
                Assert.That(BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(12, 2)), Is.EqualTo(1000));
                Assert.That(BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(14, 2)), Is.EqualTo(1001));
                Assert.That((sbyte)data[16], Is.EqualTo(5));
                Assert.That(BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(17, 2)), Is.EqualTo(2000));
                Assert.That(BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(19, 2)), Is.EqualTo(2001));
                Assert.That((sbyte)data[21], Is.EqualTo(-5));
                Assert.That(data[22], Is.EqualTo(2));
                Assert.That(data[23], Is.EqualTo(3));
                Assert.That(data[24], Is.EqualTo(0x00));
                Assert.That(data[25], Is.EqualTo(0x00));
                Assert.That(data[26], Is.EqualTo(0x01));
                Assert.That(data[27], Is.EqualTo(0x00));
                Assert.That(BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(28, 4)), Is.EqualTo(0x01020304));
                Assert.That(BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(32, 4)), Is.EqualTo(0x11121314));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(36, 2)), Is.EqualTo(0xCCCC));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(38, 2)), Is.EqualTo(0xDDDD));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(40, 2)), Is.EqualTo(0xEEEE));
                Assert.That(BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(42, 4)), Is.EqualTo(0x33333333u));
                Assert.That(data[46], Is.EqualTo(0x04));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(47, 2)), Is.EqualTo(0x9999));
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
