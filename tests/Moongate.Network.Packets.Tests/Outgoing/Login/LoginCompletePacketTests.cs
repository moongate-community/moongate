using Moongate.Network.Packets.Outgoing.Login;
using Moongate.Network.Packets.Serialization;
using Moongate.Network.Packets.Tests.Support;

namespace Moongate.Network.Packets.Tests.Outgoing.Login;

public class LoginCompletePacketTests
{
    [Fact]
    public void Encode_HeaderOnlyPacket_MatchesFixtureAndWritesAtomically()
    {
        var packet = new LoginCompletePacket();
        var expected = Convert.FromHexString("55");

        Assert.Equal(expected, PacketCodec.Encode(packet));
        PacketWriteAssertions.AssertAtomicFailure(packet);
        PacketWriteAssertions.AssertExactAndOversized(packet, expected);
    }
}
