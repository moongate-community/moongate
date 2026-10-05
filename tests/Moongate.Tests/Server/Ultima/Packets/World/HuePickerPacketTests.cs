using Moongate.Network.Packets.Serialization;
using Moongate.Server.Ultima.Packets.World;

namespace Moongate.Tests.Server.Ultima.Packets.World;

public sealed class HuePickerPacketTests
{
    [Fact]
    public void Encode_WritesTheIdTwoEmptyBytesAndTheGraphic()
    {
        var packet = new HuePickerPacket(7, 0x0FAB);

        Assert.Equal(Convert.FromHexString("950000000700000FAB"), PacketCodec.Encode(packet));
    }
}
