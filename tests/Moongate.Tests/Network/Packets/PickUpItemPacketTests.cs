using Moongate.Network.Packets.Incoming.Interaction;
using Moongate.UO.Data.Ids;

namespace Moongate.Tests.Network.Packets;

public class PickUpItemPacketTests
{
    [Test]
    public void TryParse_ShouldReadItemSerialAndStackAmount()
    {
        var packet = new PickUpItemPacket();
        var payload = new byte[]
        {
            0x07,
            0x40, 0x00, 0x00, 0x10,
            0x00, 0x0A
        };

        var parsed = packet.TryParse(payload);

        Assert.Multiple(
            () =>
            {
                Assert.That(parsed, Is.True);
                Assert.That(packet.ItemSerial, Is.EqualTo((Serial)0x40000010u));
                Assert.That(packet.StackAmount, Is.EqualTo(10));
            }
        );
    }

    [Test]
    public void TryParse_ShouldReturnFalse_WhenLengthIsInvalid()
    {
        var packet = new PickUpItemPacket();
        var payload = new byte[] { 0x07, 0x40, 0x00, 0x00, 0x10, 0x00 };

        var parsed = packet.TryParse(payload);

        Assert.That(parsed, Is.False);
    }
}
