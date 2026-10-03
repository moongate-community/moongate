using Moongate.Core.Primitives;
using Moongate.Network.Packets.Serialization;
using Moongate.Server.Ultima.Packets.World;

namespace Moongate.Tests.Server.Ultima.Packets.World;

public sealed class TextPromptPacketTests
{
    [Fact]
    public void Encode_WritesTheSerialAndTheIdAndTenZeros()
    {
        var packet = new TextPromptPacket(new Serial(2), 7);

        Assert.Equal(Convert.FromHexString("C200150000000200000007" + "00000000000000000000"), PacketCodec.Encode(packet));
    }
}
