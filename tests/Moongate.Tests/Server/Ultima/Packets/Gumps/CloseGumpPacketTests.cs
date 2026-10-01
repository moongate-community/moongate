using Moongate.Network.Packets.Serialization;
using Moongate.Server.Ultima.Packets.Gumps;

namespace Moongate.Tests.Server.Ultima.Packets.Gumps;

public sealed class CloseGumpPacketTests
{
    [Fact]
    public void Encode_WritesTheCloseSubcommand()
    {
        Assert.Equal(
            Convert.FromHexString("BF000D" + "0004" + "00000002" + "00000000"),
            PacketCodec.Encode(new CloseGumpPacket(2, 0))
        );
    }
}
