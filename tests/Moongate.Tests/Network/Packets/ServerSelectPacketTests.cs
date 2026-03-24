using Moongate.Network.Packets.Incoming.Login;

namespace Moongate.Tests.Network.Packets;

public class ServerSelectPacketTests
{
    [Test]
    public void TryParse_ShouldReadServerIndexUsingBigEndianOrder()
    {
        var packet = new ServerSelectPacket();

        var ok = packet.TryParse([0xA0, 0x00, 0x01]);

        Assert.Multiple(
            () =>
            {
                Assert.That(ok, Is.True);
                Assert.That(packet.SelectedServerIndex, Is.EqualTo(1));
            }
        );
    }
}
