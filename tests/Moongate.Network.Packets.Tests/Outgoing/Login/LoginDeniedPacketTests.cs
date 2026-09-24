using Moongate.Network.Packets.Outgoing.Login;
using Moongate.Network.Packets.Serialization;
using Moongate.Network.Packets.Tests.Support;
using Moongate.Network.Packets.Types.Login;

namespace Moongate.Network.Packets.Tests.Outgoing.Login;

public class LoginDeniedPacketTests
{
    [Fact]
    public void Encode_KnownReason_MatchesFixtureAndWritesAtomically()
    {
        var packet = new LoginDeniedPacket(LoginDeniedReason.CommunicationProblem);
        var expected = Convert.FromHexString("8204");

        Assert.Equal(LoginDeniedReason.CommunicationProblem, packet.Reason);
        Assert.Equal(expected, PacketCodec.Encode(packet));
        PacketWriteAssertions.AssertAtomicFailure(packet);
        PacketWriteAssertions.AssertExactAndOversized(packet, expected);
    }

    [Theory,
     InlineData(LoginDeniedReason.IncorrectNameOrPassword, 0x00),
     InlineData(LoginDeniedReason.AccountAlreadyInUse, 0x01),
     InlineData(LoginDeniedReason.AccountBlocked, 0x02),
     InlineData(LoginDeniedReason.InvalidCredentials, 0x03),
     InlineData(LoginDeniedReason.CommunicationProblem, 0x04),
     InlineData(LoginDeniedReason.IgrConcurrencyLimitMet, 0x05),
     InlineData(LoginDeniedReason.IgrTimeLimitMet, 0x06),
     InlineData(LoginDeniedReason.GeneralIgrAuthenticationFailure, 0x07)]
    public void Encode_DefinedReason_WritesItsProtocolByte(LoginDeniedReason reason, byte expectedReason)
    {
        var packet = new LoginDeniedPacket(reason);

        Assert.Equal(reason, packet.Reason);
        Assert.Equal(new byte[] { 0x82, expectedReason }, PacketCodec.Encode(packet));
    }
}
