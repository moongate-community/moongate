using Moongate.Network.Packets.Incoming.Interaction;

namespace Moongate.Tests.Network.Packets;

public sealed class RequestAttackPacketTests
{
    [Test]
    public void TryParse_ShouldReadTargetSerial()
    {
        var packet = new RequestAttackPacket();
        var payload = new byte[] { 0x05, 0x00, 0x00, 0x10, 0x00 };

        var parsed = packet.TryParse(payload);

        Assert.Multiple(
            () =>
            {
                Assert.That(parsed, Is.True);
                Assert.That(packet.TargetId, Is.EqualTo(0x00001000u));
            }
        );
    }

    [Test]
    public void TryParse_ShouldReturnFalse_WhenLengthIsInvalid()
    {
        var packet = new RequestAttackPacket();
        var payload = new byte[] { 0x05, 0x00, 0x00, 0x10 };

        var parsed = packet.TryParse(payload);

        Assert.That(parsed, Is.False);
    }
}
