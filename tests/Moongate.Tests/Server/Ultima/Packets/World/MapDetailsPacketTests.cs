using Moongate.Network.Packets.Serialization;
using Moongate.Server.Ultima.Data.MapItems;
using Moongate.Server.Ultima.Packets.World;

namespace Moongate.Tests.Server.Ultima.Packets.World;

public sealed class MapDetailsPacketTests
{
    private static readonly MapArea Britain = new(1092, 1396, 1736, 1924, 200, 200, 1);

    [Fact]
    public void Encode_WritesTheGumpTheAreaTheSizeAndTheFacet()
    {
        var packet = new MapDetailsPacket(0x40000001, Britain);

        Assert.Equal(Convert.FromHexString("F540000001139D0444057406C8078400C800C80001"), PacketCodec.Encode(packet));
    }

    [Fact]
    public void Encode_OfTheOldPacket_LeavesTheFacetOut()
    {
        var packet = new OldMapDetailsPacket(0x40000001, Britain);

        Assert.Equal(Convert.FromHexString("9040000001139D0444057406C8078400C800C8"), PacketCodec.Encode(packet));
    }
}
