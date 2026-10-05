using Moongate.Core.Primitives;
using Moongate.Network.Packets.Serialization;
using Moongate.Server.Ultima.Packets.Books;

namespace Moongate.Tests.Server.Ultima.Packets.Books;

public sealed class BookPagesPacketTests
{
    // ModernUO's SendBookContent: serial, page count, then each page with its number from 1, its line count and
    // its lines in UTF-8, each ended by a zero.
    [Fact]
    public void Write_IsEveryPageWithItsLines()
    {
        IReadOnlyList<IReadOnlyList<string>> pages = [["ab", "è"], [], ["c"]];

        var bytes = PacketCodec.Encode(new BookPagesPacket(new Serial(0x40000010), pages));

        Assert.Equal(
            Convert.FromHexString(
                "66" + "001D" + "40000010" + "0003" +
                "0001" + "0002" + "616200" + "C3A800" +
                "0002" + "0000" +
                "0003" + "0001" + "6300"
            ),
            bytes
        );
    }
}
