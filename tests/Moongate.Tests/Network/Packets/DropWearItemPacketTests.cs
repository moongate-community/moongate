using Moongate.Network.Packets.Incoming.Interaction;
using Moongate.Network.Spans;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Network.Packets;

public class DropWearItemPacketTests
{
    [Test]
    public void TryParse_ShouldReadItemLayerAndPlayerSerial()
    {
        var packet = new DropWearItemPacket();
        var payload = new byte[]
        {
            0x13,
            0x40, 0x00, 0x00, 0x10,
            0x04,
            0x00, 0x00, 0x00, 0x02
        };

        var parsed = packet.TryParse(payload);

        Assert.Multiple(
            () =>
            {
                Assert.That(parsed, Is.True);
                Assert.That(packet.ItemSerial, Is.EqualTo((Serial)0x40000010u));
                Assert.That(packet.Layer, Is.EqualTo(ItemLayerType.Pants));
                Assert.That(packet.PlayerSerial, Is.EqualTo((Serial)0x00000002u));
            }
        );
    }

    [Test]
    public void TryParse_ShouldReturnFalse_WhenLengthIsInvalid()
    {
        var packet = new DropWearItemPacket();
        var payload = new byte[] { 0x13, 0x00, 0x00 };

        var parsed = packet.TryParse(payload);

        Assert.That(parsed, Is.False);
    }

    [Test]
    public void Write_ShouldSerializePacket()
    {
        var packet = new DropWearItemPacket
        {
            ItemSerial = (Serial)0x40000010u,
            Layer = ItemLayerType.Pants,
            PlayerSerial = (Serial)0x00000002u
        };
        Span<byte> buffer = stackalloc byte[10];
        var writer = new SpanWriter(buffer);

        packet.Write(ref writer);

        Assert.That(
            buffer.ToArray(),
            Is.EqualTo(
                new byte[]
                {
                    0x13,
                    0x40, 0x00, 0x00, 0x10,
                    0x04,
                    0x00, 0x00, 0x00, 0x02
                }
            )
        );
    }
}
