using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Packets.General;

namespace Moongate.Tests.Server.Ultima.Packets.General;

public sealed class DropRequestPacketTests
{
    [Fact]
    public void TryParse_ReadsTheItemThePositionAndTheDestination()
    {
        Assert.True(
            DropRequestPacket.TryParse(
                Convert.FromHexString("08" + "40000012" + "002C" + "0041" + "FB" + "00" + "40000001"),
                out var packet
            )
        );

        Assert.Equal(
            (new Serial(0x40000012), (short)44, (short)65, (sbyte)-5, new Serial(0x40000001)),
            (packet.Item, packet.X, packet.Y, packet.Z, packet.Destination)
        );
    }

    [Fact]
    public void TryParse_ReadsTheGridSlotTheClientAsksFor()
    {
        Assert.True(
            DropRequestPacket.TryParse(
                Convert.FromHexString("08" + "40000012" + "002C" + "0041" + "00" + "09" + "40000001"),
                out var packet
            )
        );

        Assert.Equal(9, packet.GridIndex);
    }

    [Fact]
    public void TryParse_ADropOnTheContainerIcon_HasMinusOneCoordinates()
    {
        Assert.True(
            DropRequestPacket.TryParse(
                Convert.FromHexString("08" + "40000012" + "FFFF" + "FFFF" + "00" + "00" + "40000001"),
                out var packet
            )
        );

        Assert.Equal(((short)-1, (short)-1), (packet.X, packet.Y));
    }
}
