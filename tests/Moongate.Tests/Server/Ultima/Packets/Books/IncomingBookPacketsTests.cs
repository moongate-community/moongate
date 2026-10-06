using System.Text;
using Moongate.Server.Ultima.Packets.Books;

namespace Moongate.Tests.Server.Ultima.Packets.Books;

/// <summary>
///     What the client sends about a book, read as ModernUO's <c>BookPackets</c> reads it.
/// </summary>
public sealed class IncomingBookPacketsTests
{
    private const string Serial = "40000010";

    [Fact]
    public void Pages_AnEdit_CarriesThePageAndItsLines()
    {
        // Page 3 with two lines, then page 4 with none.
        var data = Convert.FromHexString("66" + "0017" + Serial + "0002" + "0003" + "0002" + "616200" + "C3A800" + "0004" + "0000");

        Assert.True(BookPagesRequestPacket.TryParse(data, out var packet));

        Assert.Equal(0x40000010u, packet.Book.Value);
        Assert.Equal(2, packet.Pages.Count);
        Assert.Equal((3, new[] { "ab", "è" }), (packet.Pages[0].Number, packet.Pages[0].Lines!.ToArray()));
        Assert.Equal((4, Array.Empty<string>()), (packet.Pages[1].Number, packet.Pages[1].Lines!.ToArray()));
    }

    // The client asking for page 1: a line count of 0xFFFF and no lines.
    [Fact]
    public void Pages_ARequest_HasNoLines()
    {
        var data = Convert.FromHexString("66" + "000D" + Serial + "0001" + "0001" + "FFFF");

        Assert.True(BookPagesRequestPacket.TryParse(data, out var packet));

        var page = Assert.Single(packet.Pages);
        Assert.Equal((1, null), (page.Number, page.Lines));
    }

    // Whatever cannot be a page of a book carries no page at all: nine lines, a line with no end, a page that
    // is announced and is not there, more pages than a book has. Nothing is allocated by what the packet claims.
    [Theory]
    [InlineData("0001" + "0001" + "0009" + "610061006100610061006100610061006100")]
    [InlineData("0001" + "0001" + "0001" + "6162")]
    [InlineData("0002" + "0001" + "0000")]
    [InlineData("FFFF" + "0001" + "0000")]
    [InlineData("0001" + "0001" + "0001" + "FF00")]
    public void Pages_WhatIsNotAPage_CarriesNoPages(string body)
    {
        var length = (7 + body.Length / 2).ToString("X4");
        var data = Convert.FromHexString("66" + length + Serial + body);

        Assert.True(BookPagesRequestPacket.TryParse(data, out var packet));

        Assert.Empty(packet.Pages);
    }

    [Fact]
    public void Header_CarriesTheTitleAndTheAuthor()
    {
        // serial, flags and page count skipped, then each text with the length of its bytes and its zero.
        var data = Convert.FromHexString("D4" + "001C" + Serial + "01010014" + "0007" + "43697474C3A000" + "0006" + "596F72696300");

        Assert.True(BookHeaderChangePacket.TryParse(data, out var packet));

        Assert.Equal((0x40000010u, "Città", "Yoric"), (packet.Book.Value, packet.Title, packet.Author));
    }

    [Theory]
    // A length beyond the packet, and a text that is not UTF-8.
    [InlineData("01010014" + "0030" + "4100" + "0001" + "00")]
    [InlineData("01010014" + "0003" + "FFFE00" + "0001" + "00")]
    public void Header_WhatIsNotAHeader_CarriesNoText(string body)
    {
        var length = (7 + body.Length / 2).ToString("X4");
        var data = Convert.FromHexString("D4" + length + Serial + body);

        Assert.True(BookHeaderChangePacket.TryParse(data, out var packet));

        Assert.Equal((null, null), (packet.Title, packet.Author));
    }

    [Fact]
    public void OldHeader_CarriesTheTitleInSixtyBytesAndTheAuthorInThirty()
    {
        var data = new byte[99];
        data[0] = 0x93;
        Convert.FromHexString(Serial).CopyTo(data, 1);
        Encoding.Latin1.GetBytes("My diary").CopyTo(data, 9);
        Encoding.Latin1.GetBytes("Aria").CopyTo(data, 69);

        Assert.True(OldBookHeaderChangePacket.TryParse(data, out var packet));

        Assert.Equal((0x40000010u, "My diary", "Aria"), (packet.Book.Value, packet.Title, packet.Author));
    }

    [Theory]
    [InlineData("66000540")]
    [InlineData("D4000540")]
    public void ATruncatedPacket_IsRefused(string hex)
    {
        var data = Convert.FromHexString(hex);

        Assert.False(data[0] == 0x66 ? BookPagesRequestPacket.TryParse(data, out _) : BookHeaderChangePacket.TryParse(data, out _));
    }
}
