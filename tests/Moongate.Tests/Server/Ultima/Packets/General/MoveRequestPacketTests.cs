using Moongate.Core.Types.Geometry;
using Moongate.Server.Ultima.Packets.General;

namespace Moongate.Tests.Server.Ultima.Packets.General;

public sealed class MoveRequestPacketTests
{
    [Fact]
    public void TryParse_ReadsTheDirectionTheRunningBitAndTheSequence()
    {
        Assert.True(MoveRequestPacket.TryParse(Convert.FromHexString("02820501020304"), out var packet));

        Assert.Equal(
            (DirectionType.East, true, (byte)5, 0x01020304u),
            (packet.Direction, packet.Running, packet.Sequence, packet.FastWalkKey)
        );
    }

    [Fact]
    public void TryParse_WalkingNorthWest_IsNotRunning()
    {
        Assert.True(MoveRequestPacket.TryParse(Convert.FromHexString("0207FF00000000"), out var packet));

        Assert.Equal((DirectionType.NorthWest, false, (byte)255), (packet.Direction, packet.Running, packet.Sequence));
    }
}
