using Moongate.Network.Packets.Serialization;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Server.Ultima.Types.Targeting;

namespace Moongate.Tests.Server.Ultima.Packets.World;

public sealed class TargetCursorPacketTests
{
    [Fact]
    public void Encode_ALocationCursor_WritesNineteenBytes()
    {
        var packet = new TargetCursorPacket(TargetCursorType.Location, 7, TargetFlagsType.Beneficial);

        Assert.Equal(Convert.FromHexString("6C010000000702000000000000000000000000"), PacketCodec.Encode(packet));
    }

    [Fact]
    public void Cancel_WritesFlagThreeAndIdZero()
    {
        Assert.Equal(
            Convert.FromHexString("6C000000000003000000000000000000000000"),
            PacketCodec.Encode(TargetCursorPacket.Cancel())
        );
    }
}
