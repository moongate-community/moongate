using System.Text;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Network.Spans;

namespace Moongate.Network.Packets.Incoming.Books;

[PacketHandler(0x66, PacketSizing.Variable, Description = "Books (Pages)")]

/// <summary>
/// Represents BookPagesPacket.
/// </summary>
public class BookPagesPacket : BaseGameNetworkPacket
{
    public uint BookSerial { get; set; }

    public ushort PageCount { get; set; }

    public List<BookPageEntry> Pages { get; }

    public BookPagesPacket()
        : base(0x66)
    {
        Pages = new();
    }

    public override void Write(ref SpanWriter writer)
    {
        writer.Write(OpCode);
        writer.Write((ushort)0);
        writer.Write(BookSerial);
        writer.Write((ushort)Pages.Count);

        foreach (var page in Pages)
        {
            writer.Write(page.PageNumber);

            if (page.IsPageRequest)
            {
                writer.Write((ushort)0xFFFF);

                continue;
            }

            var lineCount = page.LineCount == 0 ? (ushort)page.Lines.Count : page.LineCount;
            writer.Write(lineCount);

            for (var i = 0; i < lineCount; i++)
            {
                var line = i < page.Lines.Count ? page.Lines[i] : string.Empty;
                writer.WriteLittleUniNull(line);
            }
        }

        writer.WritePacketLength();
    }

    protected override bool ParsePayload(ref SpanReader reader)
    {
        if (reader.Remaining < 8)
        {
            return false;
        }

        var declaredLength = reader.ReadUInt16();

        if (declaredLength != reader.Length || reader.Remaining < 6)
        {
            return false;
        }

        BookSerial = reader.ReadUInt32();
        PageCount = reader.ReadUInt16();
        Pages.Clear();

        for (var i = 0; i < PageCount; i++)
        {
            if (reader.Remaining < 4)
            {
                return false;
            }

            var page = new BookPageEntry
            {
                PageNumber = reader.ReadUInt16(),
                LineCount = reader.ReadUInt16()
            };

            if (!page.IsPageRequest)
            {
                for (var lineIndex = 0; lineIndex < page.LineCount; lineIndex++)
                {
                    if (!TryReadBookLine(ref reader, out var line))
                    {
                        return false;
                    }

                    page.Lines.Add(line);
                }
            }

            Pages.Add(page);
        }

        return reader.Remaining == 0;
    }

    private static bool TryReadBookLine(ref SpanReader reader, out string value)
    {
        var buffer = reader.Buffer[reader.Position..];

        if (TryFindUnicodeTerminator(buffer, out var unicodeTerminatorIndex))
        {
            var bytes = reader.ReadBytes(unicodeTerminatorIndex + 2);
            value = unicodeTerminatorIndex == 0
                        ? string.Empty
                        : Encoding.Unicode.GetString(bytes, 0, unicodeTerminatorIndex);

            return true;
        }

        return TryReadUtf8NullTerminated(ref reader, out value);
    }

    private static bool TryReadUtf8NullTerminated(ref SpanReader reader, out string value)
    {
        value = "";

        if (reader.Remaining <= 0)
        {
            return false;
        }

        var remaining = reader.Buffer[reader.Position..];
        var terminatorIndex = remaining.IndexOf((byte)0);

        if (terminatorIndex < 0)
        {
            return false;
        }

        var bytes = reader.ReadBytes(terminatorIndex + 1);
        value = terminatorIndex == 0
                    ? string.Empty
                    : Encoding.UTF8.GetString(bytes, 0, terminatorIndex);

        return true;
    }

    private static bool TryFindUnicodeTerminator(ReadOnlySpan<byte> buffer, out int index)
    {
        index = -1;

        for (var i = 0; i + 1 < buffer.Length; i += 2)
        {
            if (buffer[i] == 0 && buffer[i + 1] == 0)
            {
                index = i;

                return true;
            }
        }

        return false;
    }
}
