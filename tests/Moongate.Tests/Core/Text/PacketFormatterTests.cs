using Moongate.Core.Text;

namespace Moongate.Tests.Core.Text;

public sealed class PacketFormatterTests
{
    private const string ColumnHeader = "        0  1  2  3  4  5  6  7  8  9  A  B  C  D  E  F";
    private const string ColumnRule = "       -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- --";

    [Fact]
    public void Format_AcrossLines_RestartsTheOffsetColumnEverySixteenBytes()
    {
        var data = new byte[20];
        data[0] = 0xA8;
        data[16] = 0xFF;

        var lines = PacketFormatter.Format(data).Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal("Opcode: 0xA8  Length: 20", lines[0]);
        Assert.StartsWith("0000   A8 00", lines[3], StringComparison.Ordinal);
        Assert.Equal("0010   FF 00 00 00", lines[4]);
        Assert.Equal(5, lines.Length);
    }

    [Fact]
    public void Format_EmptyBuffer_ReportsNoOpcodeAndOmitsTheGrid()
    {
        var formatted = PacketFormatter.Format([]);

        Assert.Equal("Opcode: (none)  Length: 0\n", formatted);
        Assert.DoesNotContain(ColumnHeader, formatted, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_ExactlyOneFullLine_DoesNotStartASecondRow()
    {
        var data = new byte[16];

        for (var i = 0; i < data.Length; i++)
        {
            data[i] = (byte)i;
        }

        var lines = PacketFormatter.Format(data).Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal("Opcode: 0x00  Length: 16", lines[0]);
        Assert.Equal("0000   00 01 02 03 04 05 06 07 08 09 0A 0B 0C 0D 0E 0F", lines[3]);
        Assert.Equal(4, lines.Length);
    }

    [Fact]
    public void Format_PartialLine_WritesTheOpcodeHeaderAndOneRowWithoutPadding()
    {
        var formatted = PacketFormatter.Format([0xBF, 0x00, 0x06, 0x00, 0x08, 0x00, 0x01]);

        Assert.Equal(
            "Opcode: 0xBF  Length: 7\n" +
            "\n" +
            ColumnHeader +
            "\n" +
            ColumnRule +
            "\n" +
            "0000   BF 00 06 00 08 00 01\n",
            formatted
        );
    }

    [Fact]
    public void Format_SingleByte_TreatsItAsAnOpcodeAndStillDumpsIt()
    {
        var formatted = PacketFormatter.Format([0x73]);

        Assert.Equal(
            "Opcode: 0x73  Length: 1\n" +
            "\n" +
            ColumnHeader +
            "\n" +
            ColumnRule +
            "\n" +
            "0000   73\n",
            formatted
        );
    }

    [Fact]
    public void Format_UsesLineFeedOnlySoOutputIsIdenticalOnEveryPlatform()
    {
        var formatted = PacketFormatter.Format([0xBF, 0x01]);

        Assert.DoesNotContain('\r', formatted);
    }
}
