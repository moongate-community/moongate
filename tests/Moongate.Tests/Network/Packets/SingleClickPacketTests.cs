using Moongate.Network.Packets.Incoming.Interaction;
using Moongate.UO.Data.Ids;

namespace Moongate.Tests.Network.Packets;

public class SingleClickPacketTests
{
    [Test]
    public void TryParse_ShouldReadTargetSerial()
    {
        var packet = new SingleClickPacket();
        var payload = new byte[]
        {
            0x09,
            0x40, 0x00, 0x00, 0x10
        };

        var parsed = packet.TryParse(payload);

        Assert.Multiple(
            () =>
            {
                Assert.That(parsed, Is.True);
                Assert.That(packet.TargetSerial, Is.EqualTo((Serial)0x40000010u));
            }
        );
    }

    [Test]
    public void TryParse_ShouldReturnFalse_WhenLengthIsInvalid()
    {
        var packet = new SingleClickPacket();
        var payload = new byte[] { 0x09, 0x40, 0x00, 0x00 };

        var parsed = packet.TryParse(payload);

        Assert.That(parsed, Is.False);
    }
}
