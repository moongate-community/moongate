using System.IO.Compression;
using System.Text;
using Moongate.Network.Attributes;
using Moongate.Network.Interfaces;
using Moongate.Network.Types;
using SquidStd.Network.Spans;

namespace Moongate.Network.Packets.Outgoing;

/// <summary>
/// Compressed gump (0xDD): the layout command string and the block of strings it indexes into, each
/// zlib-deflated.
/// <para>
/// This is the only form modern servers send — ModernUO never writes the uncompressed 0xB0, and
/// Moongate targets ClassicUO only. Each block is framed with its own compressed and uncompressed
/// lengths, and collapses to a single zero when its payload is empty. Inside the strings block every
/// entry is a big-endian character count followed by big-endian UTF-16, which is the opposite
/// endianness to most of this protocol.
/// </para>
/// </summary>
[PacketDocumentation(PacketFamilyType.Gumps, IsVariableLength = true)]
public readonly record struct CompressedGumpPacket(
    uint Serial,
    int TypeId,
    int X,
    int Y,
    string Layout,
    IReadOnlyList<string> Strings
) : IOutgoingPacket
{
    public const byte PacketId = 0xDD;

    private const int HeaderLength = 19;

    public void Write(ref SpanWriter writer)
    {
        var layout = Encoding.ASCII.GetBytes(Layout);
        var strings = PackStrings(Strings);

        var layoutBlock = Deflate(layout);
        var stringsBlock = Deflate(strings);

        writer.Write(PacketId);
        writer.Write((ushort)(HeaderLength + BlockLength(layoutBlock) + 4 + BlockLength(stringsBlock)));
        writer.Write(Serial);
        writer.Write(TypeId);
        writer.Write(X);
        writer.Write(Y);

        WriteBlock(ref writer, layoutBlock, layout.Length);

        writer.Write(Strings.Count);

        WriteBlock(ref writer, stringsBlock, strings.Length);
    }

    // An empty payload is a single zero int, not an empty block with two lengths.
    private static int BlockLength(byte[] compressed)
        => compressed.Length == 0 ? 4 : 8 + compressed.Length;

    private static byte[] Deflate(byte[] payload)
    {
        if (payload.Length == 0)
        {
            return [];
        }

        using var output = new MemoryStream();

        using (var zlib = new ZLibStream(output, CompressionLevel.Optimal, true))
        {
            zlib.Write(payload);
        }

        return output.ToArray();
    }

    /// <summary>
    /// Lays the strings end to end, each as a big-endian character count then big-endian UTF-16.
    /// </summary>
    private static byte[] PackStrings(IReadOnlyList<string> strings)
    {
        if (strings.Count == 0)
        {
            return [];
        }

        using var buffer = new MemoryStream();

        foreach (var text in strings)
        {
            buffer.WriteByte((byte)(text.Length >> 8));
            buffer.WriteByte((byte)text.Length);
            buffer.Write(Encoding.BigEndianUnicode.GetBytes(text));
        }

        return buffer.ToArray();
    }

    private static void WriteBlock(ref SpanWriter writer, byte[] compressed, int uncompressedLength)
    {
        if (compressed.Length == 0)
        {
            writer.Write(0);

            return;
        }

        writer.Write(4 + compressed.Length);
        writer.Write(uncompressedLength);
        writer.Write(compressed.AsSpan());
    }
}
