using Moongate.Core.Primitives;
using Moongate.Network.Packets.Serialization;
using Moongate.Server.Ultima.Packets.World;

namespace Moongate.Tests.Server.Ultima.Packets.World;

public sealed class DisplayContainerPacketTests
{
    [Fact]
    public void Encode_ForAHighSeasClient_AddsTheContainerType()
    {
        Assert.Equal(
            Convert.FromHexString("24" + "40000001" + "003C" + "007D"),
            PacketCodec.Encode(new DisplayContainerPacket(new Serial(0x40000001), 0x003C, true))
        );
    }

    [Fact]
    public void Encode_ForAnOlderClient_IsSevenBytes()
    {
        Assert.Equal(
            Convert.FromHexString("24" + "40000001" + "003C"),
            PacketCodec.Encode(new DisplayContainerPacket(new Serial(0x40000001), 0x003C, false))
        );
    }
}
