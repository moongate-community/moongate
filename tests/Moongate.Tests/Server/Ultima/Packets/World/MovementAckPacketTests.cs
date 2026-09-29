using Moongate.Network.Packets.Serialization;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Server.Ultima.Types.Mobiles;

namespace Moongate.Tests.Server.Ultima.Packets.World;

public sealed class MovementAckPacketTests
{
    [Fact]
    public void Encode_WritesTheSequenceAndTheNotoriety()
    {
        Assert.Equal(Convert.FromHexString("220701"), PacketCodec.Encode(new MovementAckPacket(7, NotorietyType.Innocent)));
    }
}
