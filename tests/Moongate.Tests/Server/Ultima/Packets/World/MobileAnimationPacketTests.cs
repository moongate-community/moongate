using Moongate.Core.Primitives;
using Moongate.Network.Packets.Serialization;
using Moongate.Server.Ultima.Packets.World;

namespace Moongate.Tests.Server.Ultima.Packets.World;

public sealed class MobileAnimationPacketTests
{
    [Fact]
    public void Encode_WritesTheActionItsFramesAndHowItPlays()
    {
        // mobile 0x100, action 17, 5 frames, once, forward, not repeated, no delay
        var packet = new MobileAnimationPacket(new Serial(0x100), 17, 5, 1);

        Assert.Equal(
            Convert.FromHexString("6E" + "00000100" + "0011" + "0005" + "0001" + "00" + "00" + "00"),
            PacketCodec.Encode(packet)
        );
    }

    [Fact]
    public void Encode_Backwards_Repeated_WithADelay()
    {
        var packet = new MobileAnimationPacket(new Serial(2), 32, 7, 3, false, true, 4);

        Assert.Equal(
            Convert.FromHexString("6E" + "00000002" + "0020" + "0007" + "0003" + "01" + "01" + "04"),
            PacketCodec.Encode(packet)
        );
    }
}
