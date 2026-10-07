using System.Text;
using Moongate.Core.Primitives;
using Moongate.Network.Packets.Serialization;
using Moongate.Server.Ultima.Packets.BulletinBoards;

namespace Moongate.Tests.Server.Ultima.Packets.BulletinBoards;

public sealed class BulletinBoardSummaryPacketTests
{
    private static readonly Serial Board = new(0x40000001);
    private static readonly Serial Message = new(0x40000010);

    [Fact]
    public void Encode_AFirstMessage_HasNoThread_AndThreeStringsWithTheirLengthAndZero()
    {
        var bytes = PacketCodec.Encode(
            new BulletinBoardSummaryPacket(Board, Message, Serial.Zero, "Aria", "Horse", "Oct 05, 2026")
        );

        Assert.Equal(
            Convert.FromHexString(
                "71002B" + "01" + "40000001" + "40000010" + "00000000" +
                "05" + Hex("Aria") + "00" +
                "06" + Hex("Horse") + "00" +
                "0D" + Hex("Oct 05, 2026") + "00"
            ),
            bytes
        );
    }

    [Fact]
    public void Encode_AReply_NamesItsThread()
    {
        var bytes = PacketCodec.Encode(
            new BulletinBoardSummaryPacket(Board, new Serial(0x40000011), Message, "Bruno", "Re: Horse", "Oct 06, 2026")
        );

        Assert.Equal(Convert.FromHexString("40000011" + "40000010"), bytes[8..16]);
    }

    [Fact]
    public void Encode_AnEmptyString_IsOneZero()
    {
        var bytes = PacketCodec.Encode(
            new BulletinBoardSummaryPacket(Board, Message, Serial.Zero, "", "Horse", "Oct 05, 2026")
        );

        Assert.Equal([0x01, 0x00, 0x06], bytes[16..19]);
    }

    // The length is a byte and counts the zero: 254 bytes of text at most, cut where a character ends.
    [Fact]
    public void Encode_ALongSubject_IsCutAtTwoHundredFiftyFourBytes_NeverInsideACharacter()
    {
        var subject = new string('a', 253) + "è" + "tail";

        var bytes = PacketCodec.Encode(new BulletinBoardSummaryPacket(Board, Message, Serial.Zero, "A", subject, "D"));

        // After the header and "A": the length of the subject, 253 letters and their zero.
        Assert.Equal(254, bytes[19]);
        Assert.Equal(new string('a', 253), Encoding.ASCII.GetString(bytes, 20, 253));
        Assert.Equal(0, bytes[20 + 253]);
        Assert.Equal(bytes.Length, bytes[1] << 8 | bytes[2]);
    }

    private static string Hex(string text)
    {
        return Convert.ToHexString(Encoding.UTF8.GetBytes(text));
    }
}
