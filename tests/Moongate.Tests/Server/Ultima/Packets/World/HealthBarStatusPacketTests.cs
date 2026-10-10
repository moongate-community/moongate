using Moongate.Network.Packets.Serialization;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Server.Ultima.Types.Mobiles;

namespace Moongate.Tests.Server.Ultima.Packets.World;

public sealed class HealthBarStatusPacketTests
{
    [Fact]
    public void Encode_WritesTheLengthTheSerialOneBarItsKindAndItsLevel()
    {
        var packet = new HealthBarStatusPacket(0x00000002, HealthBarType.Poison, 3);

        Assert.Equal(Convert.FromHexString("17000C00000002000100010" + "3"), PacketCodec.Encode(packet));
    }
}
