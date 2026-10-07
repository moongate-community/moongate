using Moongate.Core.Geometry;
using Moongate.Network.Packets.Serialization;
using Moongate.Server.Ultima.Packets.World;

namespace Moongate.Tests.Server.Ultima.Packets.World;

public sealed class PathfindPacketTests
{
    // ModernUO's SendPathfindMessage: the three coordinates, two bytes each.
    [Fact]
    public void Encode_IsWhereTheClientIsToWalk()
    {
        Assert.Equal(
            Convert.FromHexString("38" + "05DC" + "0640" + "000A"),
            PacketCodec.Encode(new PathfindPacket(new Point3D(1500, 1600, 10)))
        );
    }

    [Fact]
    public void Encode_ANegativeHeight_IsWrittenSigned()
    {
        Assert.Equal(
            Convert.FromHexString("38" + "05DC" + "0640" + "FFEC"),
            PacketCodec.Encode(new PathfindPacket(new Point3D(1500, 1600, -20)))
        );
    }
}
