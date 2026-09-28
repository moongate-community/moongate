using Moongate.Network.Packets.Serialization;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Packets.World;

public sealed class SeasonChangePacketTests
{
    [Fact]
    public void Encode_WritesTheExpectedBytes()
    {
        Assert.Equal(Convert.FromHexString("BC0301"), PacketCodec.Encode(new SeasonChangePacket(SeasonType.Winter, true)));
    }
}
