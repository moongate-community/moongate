using Moongate.Network.Packets.Incoming.Interaction;
using Moongate.UO.Data.Ids;

namespace Moongate.Tests.Network.Packets;

public class DropItemPacketTests
{
    [Test]
    public void TryParse_ShouldReadItemLocationAndDestination()
    {
        var packet = new DropItemPacket();
        var payload = new byte[]
        {
            0x08,
            0x40, 0x00, 0x00, 0x10,
            0x00, 0x64,
            0x00, 0xC8,
            0x0F,
            0x40, 0x00, 0x00, 0x20
        };

        var parsed = packet.TryParse(payload);

        Assert.Multiple(
            () =>
            {
                Assert.That(parsed, Is.True);
                Assert.That(packet.ItemSerial, Is.EqualTo((Serial)0x40000010u));
                Assert.That(packet.Location.X, Is.EqualTo(100));
                Assert.That(packet.Location.Y, Is.EqualTo(200));
                Assert.That(packet.Location.Z, Is.EqualTo(15));
                Assert.That(packet.DestinationSerial, Is.EqualTo((Serial)0x40000020u));
                Assert.That(packet.IsGroundDrop, Is.False);
            }
        );
    }

    [Test]
    public void TryParse_ShouldMarkGroundDrop_WhenDestinationIsMinusOne()
    {
        var packet = new DropItemPacket();
        var payload = new byte[]
        {
            0x08,
            0x40, 0x00, 0x00, 0x10,
            0x00, 0x01,
            0x00, 0x02,
            0x03,
            0xFF, 0xFF, 0xFF, 0xFF
        };

        var parsed = packet.TryParse(payload);

        Assert.Multiple(
            () =>
            {
                Assert.That(parsed, Is.True);
                Assert.That(packet.DestinationSerial, Is.EqualTo(Serial.MinusOne));
                Assert.That(packet.IsGroundDrop, Is.True);
            }
        );
    }

    [Test]
    public void TryParse_ShouldReadDestination_WhenGridByteIsPresent()
    {
        var packet = new DropItemPacket();
        var payload = new byte[]
        {
            0x08,
            0x40, 0x00, 0x00, 0x10,
            0x00, 0x64,
            0x00, 0xC8,
            0x0F,
            0x07,
            0x40, 0x00, 0x00, 0x20
        };

        var parsed = packet.TryParse(payload);

        Assert.Multiple(
            () =>
            {
                Assert.That(parsed, Is.True);
                Assert.That(packet.ItemSerial, Is.EqualTo((Serial)0x40000010u));
                Assert.That(packet.DestinationSerial, Is.EqualTo((Serial)0x40000020u));
                Assert.That(packet.IsGroundDrop, Is.False);
            }
        );
    }

    [Test]
    public void TryParse_ShouldReturnFalse_WhenLengthIsInvalid()
    {
        var packet = new DropItemPacket();
        var payload = new byte[] { 0x08, 0x00, 0x00, 0x00 };

        var parsed = packet.TryParse(payload);

        Assert.That(parsed, Is.False);
    }
}
