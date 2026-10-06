using Moongate.Network.Packets.Serialization;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Packets.World;

public sealed class WornItemPacketTests
{
    [Fact]
    public void Encode_WritesItemGraphicLayerWearerAndHue()
    {
        var shirt = new ItemEntity
            { Id = new(0x40000010), TemplateId = "shirt", ItemId = 0x1517, Amount = 1, Hue = new(0x0481) };
        shirt.Equip(new(0x00000002), LayerType.Shirt);

        var bytes = PacketCodec.Encode(new WornItemPacket(shirt));

        Assert.Equal(Convert.FromHexString("2E" + "40000010" + "1517" + "00" + "05" + "00000002" + "0481"), bytes);
    }
}
