using Moongate.Network.Packets.Incoming.Login;

namespace Moongate.Tests.Network.Packets;

public class ClientTypePacketTests
{
    [Test]
    public void TryParse_ShouldFail_WhenPayloadIsTooShort()
    {
        var packet = new ClientTypePacket();

        var ok = packet.TryParse([0xE1, 0x00, 0x05, 0x00, 0x01, 0x00, 0x00]);

        Assert.That(ok, Is.False);
    }

    [TestCase(new byte[] { 0xE1, 0x00, 0x07, 0x00, 0x01, 0x00, 0x00, 0x00, 0x02 }, 0x02u),
     TestCase(new byte[] { 0xE1, 0x00, 0x07, 0x00, 0x01, 0x00, 0x00, 0x00, 0x03 }, 0x03u)]
    public void TryParse_ShouldReadAdvertisedClientType(byte[] raw, uint advertisedClientType)
    {
        var packet = new ClientTypePacket();

        var ok = packet.TryParse(raw);

        Assert.Multiple(
            () =>
            {
                Assert.That(ok, Is.True);
                Assert.That(packet.AdvertisedClientType, Is.EqualTo(advertisedClientType));
            }
        );
    }

    [Test]
    public void TryParse_ShouldReadEnhancedClientTypeAndVersion_WhenPacketCarriesVersionString()
    {
        var packet = new ClientTypePacket();

        var ok = packet.TryParse(
            [
                0xE1,
                0x00,
                0x0D,
                0x00,
                0x03,
                (byte)'7',
                (byte)'.',
                (byte)'0',
                (byte)'.',
                (byte)'6',
                (byte)'1',
                (byte)'.',
                (byte)'0'
            ]
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(ok, Is.True);
                Assert.That(packet.AdvertisedClientType, Is.EqualTo(0x03u));
                Assert.That(packet.ResolvedClientType, Is.EqualTo(Moongate.UO.Data.Version.ClientType.SA));
                Assert.That(packet.VersionString, Is.EqualTo("7.0.61.0"));
            }
        );
    }
}
