using Moongate.Server.Ultima.Packets.General;
using Moongate.Server.Ultima.Types.Targeting;

namespace Moongate.Tests.Server.Ultima.Packets.General;

public sealed class TargetResponsePacketTests
{
    [Fact]
    public void TryParse_AGroundClick_ReadsEveryField()
    {
        // type 1, id 7, flags 0, serial 0, x 1496, y 1628, skipped 0, z 10, graphic 0x0B34
        var data = Convert.FromHexString("6C0100000007000000000005D8065C000A0B34");

        Assert.True(TargetResponsePacket.TryParse(data, out var packet));

        Assert.Equal(
            (TargetCursorType.Location, 7, 0u, (short)1496, (short)1628, (sbyte)10, (ushort)0x0B34),
            (packet.Cursor, packet.CursorId, packet.Serial.Value, packet.X, packet.Y, packet.Z, packet.Graphic)
        );
        Assert.False(packet.IsCancel);
    }

    [Fact]
    public void TryParse_ACancel_IsCancel()
    {
        var data = Convert.FromHexString("6C00000000070000000000FFFFFFFF00000000");

        Assert.True(TargetResponsePacket.TryParse(data, out var packet));
        Assert.True(packet.IsCancel);
    }

    [Fact]
    public void TryParse_TooShort_Fails()
    {
        Assert.False(TargetResponsePacket.TryParse(Convert.FromHexString("6C01000000"), out _));
    }
}
