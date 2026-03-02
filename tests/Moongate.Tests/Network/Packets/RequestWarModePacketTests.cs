using Moongate.Network.Packets.Incoming.Movement;

namespace Moongate.Tests.Network.Packets;

public class RequestWarModePacketTests
{
    [Test]
    public void TryParse_ShouldReadWarModeFlag()
    {
        var packet = new RequestWarModePacket();
        var payload = new byte[] { 0x72, 0x01, 0x00, 0x32, 0x00 };

        var parsed = packet.TryParse(payload);

        Assert.Multiple(
            () =>
            {
                Assert.That(parsed, Is.True);
                Assert.That(packet.IsWarMode, Is.True);
            }
        );
    }

    [Test]
    public void TryParse_ShouldReturnFalse_WhenLengthIsInvalid()
    {
        var packet = new RequestWarModePacket();
        var payload = new byte[] { 0x72, 0x00, 0x00, 0x32 };

        var parsed = packet.TryParse(payload);

        Assert.That(parsed, Is.False);
    }
}
