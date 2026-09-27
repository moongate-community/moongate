using Moongate.Network.Packets.Outgoing.Login;
using Moongate.Network.Packets.Serialization;
using Moongate.Network.Packets.Tests.Support;
using Moongate.Network.Packets.Types.Login;

namespace Moongate.Network.Packets.Tests.Outgoing.Login;

public class PopupMessagePacketTests
{
    [Fact]
    public void Encode_KnownMessage_MatchesFixtureAndWritesAtomically()
    {
        var packet = new PopupMessagePacket(PopupMessageType.CharacterExists);
        var expected = Convert.FromHexString("5302");

        Assert.Equal(PopupMessageType.CharacterExists, packet.Type);
        Assert.Equal(expected, PacketCodec.Encode(packet));
        PacketWriteAssertions.AssertAtomicFailure(packet);
        PacketWriteAssertions.AssertExactAndOversized(packet, expected);
    }

    [Theory,
     InlineData(PopupMessageType.IncorrectPassword, 0x00),
     InlineData(PopupMessageType.CharacterDoesNotExist, 0x01),
     InlineData(PopupMessageType.CharacterExists, 0x02),
     InlineData(PopupMessageType.CouldNotAttach, 0x03),
     InlineData(PopupMessageType.CharacterInWorld, 0x05),
     InlineData(PopupMessageType.LoginSyncError, 0x06),
     InlineData(PopupMessageType.IdleWarning, 0x07)]
    public void Encode_DefinedMessage_WritesItsProtocolByte(PopupMessageType type, byte expected)
    {
        Assert.Equal(new byte[] { 0x53, expected }, PacketCodec.Encode(new PopupMessagePacket(type)));
    }
}
