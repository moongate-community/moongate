using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Network.Packets.Serialization;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Packets.World;

namespace Moongate.Tests.Server.Ultima.Packets.World;

public sealed class ContainerContentPacketTests
{
    private static readonly Serial Backpack = new(0x40000001);

    [Fact]
    public void Encode_WithGridBytes_WritesEveryItemAtItsPositionWithItsHue()
    {
        var packet = new ContainerContentPacket([Coins()], true);

        Assert.Equal(
            Convert.FromHexString(
                "3C" + "0019" + "0001" + "40000012" + "0EED" + "00" + "00FA" + "002C" + "0041" + "00" + "40000001" + "0481"
            ),
            PacketCodec.Encode(packet)
        );
    }

    [Fact]
    public void Encode_WithGridBytes_WritesTheSlotOfEachItem()
    {
        // The Enhanced Client lays the items out by this byte: the same value for all would show one item.
        var first = Coins();
        var second = new ItemEntity { Id = new(0x40000013), TemplateId = "gold", ItemId = 0x0EED, Amount = 1 };
        second.PutInContainer(Backpack, new Point2D(44, 65), 9);

        var bytes = PacketCodec.Encode(new ContainerContentPacket([first, second], true));

        Assert.Equal((0x00, 0x09), (bytes[5 + 13], bytes[5 + 20 + 13]));
    }

    [Fact]
    public void Encode_WithoutGridBytes_LeavesThemOut()
    {
        var packet = new ContainerContentPacket([Coins()], false);

        Assert.Equal(
            Convert.FromHexString(
                "3C" + "0018" + "0001" + "40000012" + "0EED" + "00" + "00FA" + "002C" + "0041" + "40000001" + "0481"
            ),
            PacketCodec.Encode(packet)
        );
    }

    [Fact]
    public void Encode_AnEmptyContainer_SendsZeroItems()
    {
        Assert.Equal(
            Convert.FromHexString("3C" + "0005" + "0000"),
            PacketCodec.Encode(new ContainerContentPacket([], true))
        );
    }

    [Fact]
    public void Encode_AnAmountAbove65535_IsCapped()
    {
        var coins = Coins();
        coins.Amount = 100_000;

        Assert.Equal(
            "FFFF",
            Convert.ToHexString(PacketCodec.Encode(new ContainerContentPacket([coins], true)).AsSpan(12, 2))
        );
    }

    [Fact]
    public void Constructor_CopiesTheItems_SoLaterChangesAreNotSent()
    {
        var coins = Coins();
        var packet = new ContainerContentPacket([coins], true);
        coins.Amount = 1;

        Assert.Equal("00FA", Convert.ToHexString(PacketCodec.Encode(packet).AsSpan(12, 2)));
    }

    private static ItemEntity Coins()
    {
        var coins = new ItemEntity
            { Id = new(0x40000012), TemplateId = "gold", ItemId = 0x0EED, Amount = 250, Hue = new(0x0481) };
        coins.PutInContainer(Backpack, new Point2D(44, 65));

        return coins;
    }
}
