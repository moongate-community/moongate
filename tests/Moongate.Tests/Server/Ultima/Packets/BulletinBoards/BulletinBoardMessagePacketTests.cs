using System.Text;
using Moongate.Core.Primitives;
using Moongate.Network.Packets.Serialization;
using Moongate.Server.Ultima.Data.BulletinBoards;
using Moongate.Server.Ultima.Packets.BulletinBoards;

namespace Moongate.Tests.Server.Ultima.Packets.BulletinBoards;

public sealed class BulletinBoardMessagePacketTests
{
    private static readonly Serial Board = new(0x40000001);
    private static readonly Serial Message = new(0x40000010);

    [Fact]
    public void Encode_WritesThePosterItsLookAndTheLines_EachLineWithTwoZeros()
    {
        var packet = new BulletinBoardMessagePacket(
            Board,
            Message,
            "Aria",
            "Horse",
            "Oct 05, 2026",
            0x0190,
            0x83EA,
            [new BulletinEquipment(0x1F03, 0x0021), new BulletinEquipment(0x203B, 0x044E)],
            ["Selling a horse", "", "Ask Aria"]
        );

        var bytes = PacketCodec.Encode(packet);

        var expected = "71" + "LLLL" + "02" + "40000001" + "40000010" +
                       "05" + Hex("Aria") + "00" +
                       "06" + Hex("Horse") + "00" +
                       "0D" + Hex("Oct 05, 2026") + "00" +
                       "0190" + "83EA" +
                       "02" + "1F03" + "0021" + "203B" + "044E" +
                       "03" +
                       "11" + Hex("Selling a horse") + "0000" +
                       "02" + "0000" +
                       "0A" + Hex("Ask Aria") + "0000";
        expected = expected.Replace("LLLL", (expected.Length / 2).ToString("X4"));

        Assert.Equal(Convert.FromHexString(expected), bytes);
    }

    [Fact]
    public void Encode_WithNothingWorn_HasACountOfZero()
    {
        var bytes = PacketCodec.Encode(new BulletinBoardMessagePacket(Board, Message, "A", "S", "D", 0x0190, 0, [], ["x"]));

        // Header 12, three strings of one letter (3 bytes each), body and hue.
        Assert.Equal([0x00, 0x01, 0x03, (byte)'x', 0x00, 0x00], bytes[25..31]);
        Assert.Equal(31, bytes.Length);
    }

    // A line's length counts its two zeros: 253 bytes of text at most.
    [Fact]
    public void Encode_ALongLine_IsCutAtTwoHundredFiftyThreeBytes_NeverInsideACharacter()
    {
        var line = new string('a', 252) + "è" + "tail";

        var bytes = PacketCodec.Encode(new BulletinBoardMessagePacket(Board, Message, "A", "S", "D", 0x0190, 0, [], [line]));

        Assert.Equal(254, bytes[27]);
        Assert.Equal(new string('a', 252), Encoding.ASCII.GetString(bytes, 28, 252));
        Assert.Equal([0, 0], bytes[280..282]);
        Assert.Equal(282, bytes.Length);
    }

    [Fact]
    public void Encode_MoreThanTwoHundredFiftyFiveLinesOrPieces_SendsTheFirstTwoHundredFiftyFive()
    {
        var lines = Enumerable.Range(0, 300).Select(_ => "x").ToArray();
        var worn = Enumerable.Range(0, 300).Select(index => new BulletinEquipment(index, 0)).ToArray();

        var bytes = PacketCodec.Encode(new BulletinBoardMessagePacket(Board, Message, "A", "S", "D", 0x0190, 0, worn, lines));

        Assert.Equal(255, bytes[25]);
        Assert.Equal(255, bytes[26 + 255 * 4]);
        Assert.Equal(26 + 255 * 4 + 1 + 255 * 4, bytes.Length);
        Assert.Equal(bytes.Length, bytes[1] << 8 | bytes[2]);
    }

    private static string Hex(string text)
    {
        return Convert.ToHexString(Encoding.UTF8.GetBytes(text));
    }
}
