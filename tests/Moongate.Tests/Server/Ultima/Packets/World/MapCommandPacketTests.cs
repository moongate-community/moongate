using Moongate.Network.Packets.Serialization;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Server.Ultima.Types.MapItems;

namespace Moongate.Tests.Server.Ultima.Packets.World;

public sealed class MapCommandPacketTests
{
    [Fact]
    public void Encode_WritesTheCommandTheFlagAndThePoint()
    {
        var packet = new MapCommandPacket(0x40000001, MapCommandType.AddPin, false, 10, 20);

        Assert.Equal(Convert.FromHexString("56400000010100000A0014"), PacketCodec.Encode(packet));
    }

    [Fact]
    public void Encode_OfTheEditableAnswer_SetsTheFlag()
    {
        var packet = new MapCommandPacket(0x40000001, MapCommandType.EditableAnswer, true, 0, 0);

        Assert.Equal(Convert.FromHexString("5640000001070100000000"), PacketCodec.Encode(packet));
    }
}
