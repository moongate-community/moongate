using Moongate.Core.Geometry;
using Moongate.Core.Types.Geometry;
using Moongate.Network.Packets.Serialization;
using Moongate.Server.Ultima.Packets.World;

namespace Moongate.Tests.Server.Ultima.Packets.World;

public sealed class MovementRejectPacketTests
{
    [Fact]
    public void Encode_WritesTheSequenceAndTheRealPosition()
    {
        var packet = new MovementRejectPacket(9, new Point3D(1496, 1628, -5), DirectionType.West);

        Assert.Equal(Convert.FromHexString("2109" + "05D8" + "065C" + "06" + "FB"), PacketCodec.Encode(packet));
    }
}
