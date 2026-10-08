using Moongate.Core.Primitives;
using Moongate.Network.Packets.Serialization;
using Moongate.Server.Ultima.Packets.Vendors;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Packets.Vendors;

public sealed class VendorPacketsTests
{
    [Fact]
    public void Encode_TheEndOfTheWindow_IsTheVendorAndAZeroCount()
    {
        var bytes = PacketCodec.Encode(new VendorEndPacket(new Serial(0x00000102)));

        Assert.Equal(Convert.FromHexString("3B0008" + "00000102" + "00"), bytes);
    }

    [Fact]
    public void Encode_TheBuyList_WritesThePriceAndTheNameOfEveryLine()
    {
        var bytes = PacketCodec.Encode(
            new VendorBuyListPacket(
                new Serial(0x7FFFFFFF),
                [new VendorBuyListEntry(8, "1020000"), new VendorBuyListEntry(300, "Loaf")]
            )
        );

        Assert.Equal(
            Convert.FromHexString(
                "74" + "001F" + "7FFFFFFF" + "02" +
                "00000008" + "08" + "31303230303030" + "00" +
                "0000012C" + "05" + "4C6F6166" + "00"
            ),
            bytes
        );
    }

    [Fact]
    public void Encode_ANameOfMoreThanTheLengthByteHolds_IsCut()
    {
        var bytes = PacketCodec.Encode(
            new VendorBuyListPacket(new Serial(0x7FFFFFFF), [new VendorBuyListEntry(1, new string('a', 400))])
        );

        Assert.Equal(8 + 6 + 253, bytes.Length);
        Assert.Equal(254, bytes[12]);
    }

    [Fact]
    public void Encode_TheShopContainerWornByTheVendor_IsAnEquipUpdate()
    {
        var bytes = PacketCodec.Encode(
            new WornItemPacket(new Serial(0x7FFFFFFF), 0, LayerType.ShopBuy, new Serial(0x00000102), 0)
        );

        Assert.Equal(Convert.FromHexString("2E" + "7FFFFFFF" + "0000" + "001A" + "00000102" + "0000"), bytes);
    }

    [Fact]
    public void TryParse_TheReplyOfTwoLines_GivesTheVendorAndTheLines()
    {
        Assert.True(
            VendorBuyReplyPacket.TryParse(
                Convert.FromHexString("3B0016" + "00000102" + "02" + "1A7FFFFFFE0003" + "1A7FFFFFFF0001"),
                out var packet
            )
        );

        Assert.Equal((new Serial(0x102), VendorBuyReplyPacket.BuyFlag), (packet.Vendor, packet.Flag));
        Assert.Equal(
            [
                new VendorBuyReplyLine(0x1A, new Serial(0x7FFFFFFE), 3),
                new VendorBuyReplyLine(0x1A, new Serial(0x7FFFFFFF), 1)
            ],
            packet.Lines
        );
    }

    [Fact]
    public void TryParse_ACancel_HasNoLines()
    {
        Assert.True(VendorBuyReplyPacket.TryParse(Convert.FromHexString("3B0008" + "00000102" + "00"), out var packet));
        Assert.Empty(packet.Lines);
    }

    [Theory,
     InlineData("3B000F" + "00000102" + "02" + "1A7FFFFF"),
     InlineData("3B0009" + "00000102" + "02" + "00")]
    public void TryParse_ATruncatedLine_IsRefused(string hex)
    {
        Assert.False(VendorBuyReplyPacket.TryParse(Convert.FromHexString(hex), out _));
    }

    [Fact]
    public void Encode_TheSellList_WritesEachItemWithItsPriceAndName()
    {
        var bytes = PacketCodec.Encode(
            new VendorSellListPacket(
                new Serial(0x100),
                [new VendorSellListEntry(new Serial(0x40000001), 0x103B, 0x0044, 3, 4, "bread")]
            )
        );

        Assert.Equal(
            Convert.FromHexString(
                "9E" + "001C" + "00000100" + "0001" + "40000001" + "103B" + "0044" + "0003" + "0004" + "0005" + "627265" +
                "6164"
            ),
            bytes
        );
    }

    [Fact]
    public void Encode_ThePriceAndTheAmountAboveAShort_AreCappedAndAnAccentIsAQuestionMark()
    {
        var bytes = PacketCodec.Encode(
            new VendorSellListPacket(
                new Serial(0x100),
                [new VendorSellListEntry(new Serial(0x40000001), 1, 0, 70000, 100000, "è")]
            )
        );

        Assert.Equal(Convert.FromHexString("FFFF" + "FFFF" + "0001" + "3F"), bytes[17..]);
    }

    [Fact]
    public void TryParse_TheSellReply_GivesTheItemsAndTheAmounts()
    {
        Assert.True(
            VendorSellReplyPacket.TryParse(
                Convert.FromHexString("9F0015" + "00000100" + "0002" + "400000010003" + "400000020001"),
                out var packet
            )
        );

        Assert.Equal(new Serial(0x100), packet.Vendor);
        Assert.Equal(
            [new VendorSellReplyLine(new Serial(0x40000001), 3), new VendorSellReplyLine(new Serial(0x40000002), 1)],
            packet.Lines
        );
    }

    [Theory,
     InlineData("9F000F" + "00000100" + "0002" + "400000010003"),
     InlineData("9F0015" + "00000100" + "0001" + "400000010003" + "400000020001")]
    public void TryParse_ACountThatDoesNotMatchTheSize_IsRefused(string hex)
    {
        Assert.False(VendorSellReplyPacket.TryParse(Convert.FromHexString(hex), out _));
    }
}
