using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using Moongate.Network.Packets.Incoming;
using Moongate.Network.Packets.Outgoing;
using SquidStd.Network.Spans;

namespace Moongate.Tests.Network.Packets;

/// <summary>
/// The gump packets are the one place in this feature where a mistake is invisible: a wrong length or
/// endianness renders a blank gump rather than failing, so the assertions here go down to the bytes.
/// </summary>
public class GumpPacketTests
{
    [Fact]
    public void CompressedGump_WritesTheHeaderAndRoundTripsTheLayout()
    {
        const string layout = "{ resizepic 0 0 5054 300 200 }";
        var packet = new CompressedGumpPacket(0x1234u, 0x5678, 100, 200, layout, []);
        var buffer = new byte[1024];
        var writer = new SpanWriter(buffer);

        packet.Write(ref writer);

        var span = buffer.AsSpan(0, writer.Position);

        Assert.Equal(0xDD, span[0]);
        Assert.Equal(writer.Position, BinaryPrimitives.ReadUInt16BigEndian(span[1..]));
        Assert.Equal(0x1234u, BinaryPrimitives.ReadUInt32BigEndian(span[3..]));
        Assert.Equal(0x5678, BinaryPrimitives.ReadInt32BigEndian(span[7..]));
        Assert.Equal(100, BinaryPrimitives.ReadInt32BigEndian(span[11..]));
        Assert.Equal(200, BinaryPrimitives.ReadInt32BigEndian(span[15..]));

        // The block frames itself: 4 + compressed length, then the uncompressed length.
        var blockLength = BinaryPrimitives.ReadInt32BigEndian(span[19..]);
        Assert.Equal(layout.Length, BinaryPrimitives.ReadInt32BigEndian(span[23..]));

        var compressed = span.Slice(27, blockLength - 4);
        Assert.Equal(layout, Inflate(compressed, layout.Length, Encoding.ASCII));
    }

    // An empty payload is not an empty block: it collapses to a single zero int, and getting that
    // wrong is what makes a gump with no text fail while one with text works.
    [Fact]
    public void CompressedGump_WithNoStrings_CollapsesTheBlockToZero()
    {
        var packet = new CompressedGumpPacket(1u, 2, 0, 0, "{ page 0 }", []);
        var buffer = new byte[1024];
        var writer = new SpanWriter(buffer);

        packet.Write(ref writer);

        var span = buffer.AsSpan(0, writer.Position);
        var layoutBlockLength = BinaryPrimitives.ReadInt32BigEndian(span[19..]);
        var stringsCountAt = 19 + 4 + layoutBlockLength;

        Assert.Equal(0, BinaryPrimitives.ReadInt32BigEndian(span[stringsCountAt..]));
        Assert.Equal(0, BinaryPrimitives.ReadInt32BigEndian(span[(stringsCountAt + 4)..]));
        Assert.Equal(stringsCountAt + 8, writer.Position);
    }

    // Each string is framed big-endian, opposite to most of this protocol.
    [Fact]
    public void CompressedGump_FramesEachStringBigEndian()
    {
        var packet = new CompressedGumpPacket(1u, 2, 0, 0, "{ page 0 }", ["ciao"]);
        var buffer = new byte[1024];
        var writer = new SpanWriter(buffer);

        packet.Write(ref writer);

        var span = buffer.AsSpan(0, writer.Position);
        var layoutBlockLength = BinaryPrimitives.ReadInt32BigEndian(span[19..]);
        var countAt = 19 + 4 + layoutBlockLength;

        Assert.Equal(1, BinaryPrimitives.ReadInt32BigEndian(span[countAt..]));

        var stringsBlockLength = BinaryPrimitives.ReadInt32BigEndian(span[(countAt + 4)..]);
        var uncompressed = BinaryPrimitives.ReadInt32BigEndian(span[(countAt + 8)..]);
        var inflated = InflateRaw(span.Slice(countAt + 12, stringsBlockLength - 4), uncompressed);

        Assert.Equal(4, BinaryPrimitives.ReadUInt16BigEndian(inflated));
        Assert.Equal("ciao", Encoding.BigEndianUnicode.GetString(inflated.AsSpan(2)));
    }

    [Fact]
    public void GumpMenuSelection_ReadsSwitchesAndTextEntries()
    {
        var buffer = new byte[256];
        var writer = new SpanWriter(buffer);

        writer.Write((byte)0xB1);
        writer.Write((ushort)0); // length, unread
        writer.Write(0x1234u);   // serial
        writer.Write(0x5678);    // typeId
        writer.Write(7);         // button
        writer.Write(2);         // switch count
        writer.Write(3);
        writer.Write(9);
        writer.Write(1);         // text entry count
        writer.Write((ushort)5); // entry id
        writer.Write((ushort)4); // characters
        writer.WriteBigUni("ciao");

        var reader = new SpanReader(buffer.AsSpan(0, writer.Position));
        var packet = GumpMenuSelectionPacket.Read(ref reader);

        Assert.Equal(0x1234u, packet.Serial);
        Assert.Equal(0x5678, packet.TypeId);
        Assert.Equal(7, packet.ButtonId);
        Assert.Equal([3, 9], packet.Switches);
        Assert.Equal("ciao", packet.TextEntries[5]);
    }

    private static string Inflate(ReadOnlySpan<byte> compressed, int length, Encoding encoding)
        => encoding.GetString(InflateRaw(compressed, length));

    private static byte[] InflateRaw(ReadOnlySpan<byte> compressed, int length)
    {
        using var source = new MemoryStream(compressed.ToArray());
        using var zlib = new ZLibStream(source, CompressionMode.Decompress);
        var output = new byte[length];
        zlib.ReadExactly(output);

        return output;
    }
}
