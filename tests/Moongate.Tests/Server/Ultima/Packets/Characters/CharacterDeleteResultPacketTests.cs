using Moongate.Network.Packets.Serialization;
using Moongate.Server.Ultima.Packets.Characters;
using Moongate.Server.Ultima.Types.Characters;

namespace Moongate.Tests.Server.Ultima.Packets.Characters;

public sealed class CharacterDeleteResultPacketTests
{
    [Theory]
    [InlineData(CharacterDeleteResultType.IncorrectPassword, 0x00)]
    [InlineData(CharacterDeleteResultType.CharacterDoesNotExist, 0x01)]
    [InlineData(CharacterDeleteResultType.CharacterBeingPlayed, 0x02)]
    [InlineData(CharacterDeleteResultType.CharacterNotOldEnough, 0x03)]
    [InlineData(CharacterDeleteResultType.CharacterQueuedForBackup, 0x04)]
    [InlineData(CharacterDeleteResultType.RequestFailed, 0x05)]
    public void Encode_WritesOpcodeAndReason(CharacterDeleteResultType result, byte expected)
    {
        Assert.Equal(new byte[] { 0x85, expected }, PacketCodec.Encode(new CharacterDeleteResultPacket(result)));
    }
}
