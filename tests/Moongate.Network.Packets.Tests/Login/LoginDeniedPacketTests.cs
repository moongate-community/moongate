using Moongate.Network.Packets.Login;
using Moongate.Network.Packets.Serialization;
using Moongate.Network.Packets.Tests.Support;

namespace Moongate.Network.Packets.Tests.Login;

public class LoginDeniedPacketTests
{
    [Fact]
    public void Encode_KnownReason_MatchesFixtureAndWritesAtomically()
    {
        var packet = new LoginDeniedPacket(0x04);
        var expected = Convert.FromHexString("8204");

        Assert.Equal((byte)0x04, packet.Reason);
        Assert.Equal(expected, PacketCodec.Encode(packet));
        PacketWriteAssertions.AssertAtomicFailure(packet);
        PacketWriteAssertions.AssertExactAndOversized(packet, expected);
    }
}
