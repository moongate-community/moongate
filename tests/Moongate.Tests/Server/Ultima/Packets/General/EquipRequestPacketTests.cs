using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Packets.General;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Packets.General;

public sealed class EquipRequestPacketTests
{
    [Fact]
    public void TryParse_ReadsTheItemTheLayerAndTheMobile()
    {
        Assert.True(EquipRequestPacket.TryParse(Convert.FromHexString("13" + "40000012" + "05" + "00000002"), out var packet));

        Assert.Equal((new Serial(0x40000012), LayerType.Shirt, new Serial(0x00000002)), (packet.Item, packet.Layer, packet.Mobile));
    }
}
