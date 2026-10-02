using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Network.Packets.Serialization;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Packets.World;

namespace Moongate.Tests.Server.Ultima.Packets.World;

public sealed class ContainerItemUpdatePacketTests
{
    [Fact]
    public void Encode_WithTheGridByte_IsTwentyOneBytes()
    {
        Assert.Equal(
            Convert.FromHexString("25" + "40000012" + "0EED" + "00" + "00FA" + "002C" + "0041" + "00" + "40000001" + "0481"),
            PacketCodec.Encode(new ContainerItemUpdatePacket(Coins(), true))
        );
    }

    [Fact]
    public void Encode_WithTheGridByte_WritesTheSlotOfTheItem()
    {
        var coins = Coins();
        coins.PutInContainer(coins.ContainerId!.Value, coins.GridLocation!.Value, 9);

        Assert.Equal(0x09, PacketCodec.Encode(new ContainerItemUpdatePacket(coins, true))[14]);
    }

    [Fact]
    public void Encode_WithoutTheGridByte_IsTwentyBytes()
    {
        Assert.Equal(
            Convert.FromHexString("25" + "40000012" + "0EED" + "00" + "00FA" + "002C" + "0041" + "40000001" + "0481"),
            PacketCodec.Encode(new ContainerItemUpdatePacket(Coins(), false))
        );
    }

    [Fact]
    public void Constructor_CopiesTheItem_SoLaterChangesAreNotSent()
    {
        var coins = Coins();
        var packet = new ContainerItemUpdatePacket(coins, true);
        coins.PutInContainer(new Serial(0x40000099), new Point2D(1, 1));

        Assert.Equal("40000001", Convert.ToHexString(PacketCodec.Encode(packet).AsSpan(15, 4)));
    }

    private static ItemEntity Coins()
    {
        var coins = new ItemEntity { Id = new(0x40000012), TemplateId = "gold", ItemId = 0x0EED, Amount = 250, Hue = new(0x0481) };
        coins.PutInContainer(new Serial(0x40000001), new Point2D(44, 65));

        return coins;
    }
}
