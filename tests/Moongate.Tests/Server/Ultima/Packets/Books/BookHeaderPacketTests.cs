using System.Text;
using Moongate.Core.Primitives;
using Moongate.Network.Packets.Serialization;
using Moongate.Server.Ultima.Packets.Books;

namespace Moongate.Tests.Server.Ultima.Packets.Books;

public sealed class BookHeaderPacketTests
{
    // ModernUO's SendBookCover: serial, flag 1, not writable, pages, then title and author with their lengths.
    [Fact]
    public void Write_IsTheCoverOfAReadOnlyBook()
    {
        var bytes = PacketCodec.Encode(new BookHeaderPacket(new Serial(0x40000010), 12, "Orcish", "Yorick"));

        Assert.Equal(
            Convert.FromHexString(
                "D4" + "001D" + "40000010" + "01" + "00" + "000C" + "0007" + "4F726369736800" + "0007" + "596F7269636B00"
            ),
            bytes
        );
    }

    [Fact]
    public void Write_AWritableBook_SaysSo()
    {
        var bytes = PacketCodec.Encode(new BookHeaderPacket(new Serial(0x40000010), 20, "a book", "Aria", true));

        Assert.Equal([0x01, 0x01, 0x00, 0x14], bytes[7..11]);
    }

    [Fact]
    public void Write_TextIsUtf8_AndItsLengthCountsBytesAndTheZero()
    {
        var bytes = PacketCodec.Encode(new BookHeaderPacket(new Serial(0x40000010), 1, "Città", ""));

        // "Città" is six bytes in UTF-8, seven with its zero; an empty author is the zero alone.
        Assert.Equal([0x00, 0x07], bytes[11..13]);
        Assert.Equal("Città\0", Encoding.UTF8.GetString(bytes, 13, 7));
        Assert.Equal([0x00, 0x01, 0x00], bytes[20..23]);
        Assert.Equal(bytes.Length, bytes[1] << 8 | bytes[2]);
    }

    // The client's fields hold 60 and 30 bytes: a longer text is cut before the character that does not fit.
    [Fact]
    public void Write_ALongTitleOrAuthor_IsCutOnACharacterBoundary()
    {
        var title = new string('a', 59) + "è" + "tail";
        var author = new string('b', 29) + "è";

        var bytes = PacketCodec.Encode(new BookHeaderPacket(new Serial(0x40000010), 1, title, author));

        Assert.Equal(59 + 1, bytes[11] << 8 | bytes[12]);
        Assert.Equal(new string('a', 59) + "\0", Encoding.UTF8.GetString(bytes, 13, 60));
        Assert.Equal(29 + 1, bytes[73] << 8 | bytes[74]);
        Assert.Equal(new string('b', 29) + "\0", Encoding.UTF8.GetString(bytes, 75, 30));
    }
}
