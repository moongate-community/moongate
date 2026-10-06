using Moongate.Network.Packets.Serialization;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Server.Ultima.Types.Items;

namespace Moongate.Tests.Server.Ultima.Packets.World;

public sealed class LiftRejectPacketTests
{
    [Fact]
    public void Encode_WritesTheReason()
    {
        Assert.Equal(
            Convert.FromHexString("2704"),
            PacketCodec.Encode(new LiftRejectPacket(LiftRejectReasonType.AreHolding))
        );
    }
}
