using Moongate.Server.Ultima.Packets.General;
using Moongate.Server.Ultima.Types.MapItems;

namespace Moongate.Tests.Server.Ultima.Packets.General;

public sealed class MapCommandRequestPacketTests
{
    [Fact]
    public void TryParse_ReadsTheSerialTheCommandTheNumberAndThePoint()
    {
        Assert.True(MapCommandRequestPacket.TryParse(Convert.FromHexString("5640000001020300050006"), out var packet));

        Assert.Equal(
            (0x40000001u, MapCommandType.InsertPin, 3, 5, 6),
            (packet.Serial, packet.Command, packet.Number, packet.X, packet.Y)
        );
    }

    [Fact]
    public void TryParse_OfAShortBuffer_Fails()
    {
        Assert.False(MapCommandRequestPacket.TryParse(Convert.FromHexString("56400000010203"), out _));
    }
}
