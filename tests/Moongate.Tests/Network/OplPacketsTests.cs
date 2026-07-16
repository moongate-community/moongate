using Moongate.Core.Primitives;
using Moongate.Network.Data;
using Moongate.Network.Packets.Incoming;
using Moongate.Network.Packets.Outgoing;
using SquidStd.Network.Spans;

namespace Moongate.Tests.Network;

public class OplPacketsTests
{
    [Fact]
    public void Request_Read_ParsesSerialList()
    {
        var bytes = new byte[] { 0xD6, 0x00, 0x0B, 0x00, 0x00, 0x00, 0x01, 0x40, 0x00, 0x00, 0x02 };
        var reader = new SpanReader(bytes);

        var packet = MegaClilocRequestPacket.Read(ref reader);

        Assert.Equal(2, packet.Serials.Count);
        Assert.Equal(new Serial(0x00000001), packet.Serials[0]);
        Assert.Equal(new Serial(0x40000002), packet.Serials[1]);
    }

    [Fact]
    public void Request_Read_MalformedLength_ReturnsEmpty()
    {
        // Length 6 means a 3-byte payload, which is not a multiple of four.
        var bytes = new byte[] { 0xD6, 0x00, 0x06, 0x00, 0x00, 0x00 };
        var reader = new SpanReader(bytes);

        var packet = MegaClilocRequestPacket.Read(ref reader);

        Assert.Empty(packet.Serials);
    }

    [Fact]
    public void Request_Read_HeaderOnly_ReturnsEmpty()
    {
        var bytes = new byte[] { 0xD6, 0x00, 0x03 };
        var reader = new SpanReader(bytes);

        var packet = MegaClilocRequestPacket.Read(ref reader);

        Assert.Empty(packet.Serials);
    }

    [Fact]
    public void MegaCliloc_Write_EncodesEntriesAndTerminator()
    {
        var entries = new[] { new OplEntry(1050039, "3\tgold coin"), new OplEntry(1042971, "shiny") };
        var packet = new MegaClilocPacket(new Serial(0x40000001), 0x1234ABCD, entries);

        var writer = new SpanWriter(stackalloc byte[63]);
        packet.Write(ref writer);
        var written = writer.Span.ToArray();

        // total = 15 header + (6 + 11*2) + (6 + 5*2) + 4 terminator = 63
        Assert.Equal(63, written.Length);
        Assert.Equal(0xD6, written[0]);
        Assert.Equal((ushort)63, (ushort)((written[1] << 8) | written[2]));
        Assert.Equal((ushort)1, (ushort)((written[3] << 8) | written[4]));
        Assert.Equal(0x40000001u, (uint)((written[5] << 24) | (written[6] << 16) | (written[7] << 8) | written[8]));
        Assert.Equal((ushort)0, (ushort)((written[9] << 8) | written[10]));

        // Raw hash, NOT flagged with 0x40000000.
        Assert.Equal(
            0x1234ABCDu,
            (uint)((written[11] << 24) | (written[12] << 16) | (written[13] << 8) | written[14])
        );

        // First entry: cliloc, byte length, UTF-16LE text (low byte first).
        Assert.Equal(
            1050039u,
            (uint)((written[15] << 24) | (written[16] << 16) | (written[17] << 8) | written[18])
        );
        Assert.Equal((ushort)22, (ushort)((written[19] << 8) | written[20]));
        Assert.Equal((byte)'3', written[21]);
        Assert.Equal((byte)0, written[22]);
        Assert.Equal((byte)'\t', written[23]);

        // Trailing 4-byte zero terminator.
        Assert.Equal(0u, (uint)((written[^4] << 24) | (written[^3] << 16) | (written[^2] << 8) | written[^1]));
    }

    [Fact]
    public void OplInfo_Write_IsNineBytesWithFlaggedHash()
    {
        var packet = new OplInfoPacket(new Serial(0x00000007), 0x00000012);

        var writer = new SpanWriter(stackalloc byte[9]);
        packet.Write(ref writer);
        var written = writer.Span.ToArray();

        Assert.Equal(9, written.Length);
        Assert.Equal(0xDC, written[0]);
        Assert.Equal(0x00000007u, (uint)((written[1] << 24) | (written[2] << 16) | (written[3] << 8) | written[4]));
        Assert.Equal(0x40000012u, (uint)((written[5] << 24) | (written[6] << 16) | (written[7] << 8) | written[8]));
    }
}
