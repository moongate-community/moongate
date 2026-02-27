using System.Buffers.Binary;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Outgoing.World;
using Moongate.Network.Spans;
using Moongate.UO.Data.Geometry;

namespace Moongate.Tests.Network.Packets;

public class PlaySoundEffectPacketTests
{
    [Test]
    public void TryParse_ShouldReadFields()
    {
        var packet = new PlaySoundEffectPacket();
        var payload = new byte[]
        {
            0x54,
            0x00,
            0x01, 0x02,
            0x03, 0x04,
            0x05, 0x06,
            0x07, 0x08,
            0xFF, 0xFE
        };

        var parsed = packet.TryParse(payload);

        Assert.Multiple(
            () =>
            {
                Assert.That(parsed, Is.True);
                Assert.That(packet.Mode, Is.EqualTo(0x00));
                Assert.That(packet.SoundModel, Is.EqualTo(0x0102));
                Assert.That(packet.Unknown3, Is.EqualTo(0x0304));
                Assert.That(packet.Location, Is.EqualTo(new Point3D(0x0506, 0x0708, -2)));
            }
        );
    }

    [Test]
    public void Write_ShouldSerializeExpectedPayload()
    {
        var packet = new PlaySoundEffectPacket(
            0x01,
            0x0203,
            0x0405,
            new(0x0607, 0x0809, 0x0A0B)
        );

        var data = Write(packet);

        Assert.Multiple(
            () =>
            {
                Assert.That(data.Length, Is.EqualTo(12));
                Assert.That(data[0], Is.EqualTo(0x54));
                Assert.That(data[1], Is.EqualTo(0x01));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(2, 2)), Is.EqualTo(0x0203));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(4, 2)), Is.EqualTo(0x0405));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(6, 2)), Is.EqualTo(0x0607));
                Assert.That(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(8, 2)), Is.EqualTo(0x0809));
                Assert.That(BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(10, 2)), Is.EqualTo(0x0A0B));
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
