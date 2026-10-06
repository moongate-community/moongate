using Moongate.Network.Packets.Serialization;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Packets.World;

public sealed class CorpseEquipmentPacketTests
{
    [Fact]
    public void Encode_WritesEachLayerPlusOneWithItsSerial_AndAZeroToEnd()
    {
        var packet = new CorpseEquipmentPacket(
            new(0x40000900),
            [new(LayerType.Shirt, new(0x40000004)), new(LayerType.Hair, new(0x7FFFF000))]
        );

        // length 18; shirt is layer 5, written 6; hair is layer 11, written 12
        Assert.Equal(Convert.FromHexString("890012400009000640000004" + "0C7FFFF000" + "00"), PacketCodec.Encode(packet));
    }

    [Fact]
    public void Encode_ACorpseWearingNothing_IsTheHeaderAndTheEnd()
    {
        Assert.Equal(
            Convert.FromHexString("89000840000900" + "00"),
            PacketCodec.Encode(new CorpseEquipmentPacket(new(0x40000900), []))
        );
    }
}
