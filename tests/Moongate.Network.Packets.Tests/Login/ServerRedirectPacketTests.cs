using System.Net;

using Moongate.Network.Packets.Login;
using Moongate.Network.Packets.Serialization;
using Moongate.Network.Packets.Tests.Support;

namespace Moongate.Network.Packets.Tests.Login;

public class ServerRedirectPacketTests
{
    [Fact]
    public void Encode_KnownAddressPortAndKey_MatchesNormalIpOrderFixture()
    {
        var addressBytes = new byte[] { 192, 168, 0, 206 };
        var address = new IPAddress(addressBytes);
        var packet = new ServerRedirectPacket(address, 2593, 0x12345678);
        addressBytes[0] = 1;
        var expected = Convert.FromHexString("8CC0A800CE0A2112345678");

        Assert.Equal(IPAddress.Parse("192.168.0.206"), packet.Address);
        Assert.Equal((ushort)2593, packet.Port);
        Assert.Equal(0x12345678u, packet.AuthKey);
        Assert.Equal(expected, PacketCodec.Encode(packet));
        Assert.Equal(expected, PacketCodec.Encode(packet));
        PacketWriteAssertions.AssertAtomicFailure(packet);
        PacketWriteAssertions.AssertExactAndOversized(packet, expected);
    }

    [Fact]
    public void Constructor_IPv6Address_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new ServerRedirectPacket(IPAddress.IPv6Loopback, 2593, 1));
    }
}
