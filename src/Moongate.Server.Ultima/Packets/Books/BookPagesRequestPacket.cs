using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using Moongate.Core.Primitives;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Server.Ultima.Data.Books;
using Moongate.Server.Ultima.Packets.Books.Internal;
using Moongate.Server.Ultima.Services.Books;

namespace Moongate.Server.Ultima.Packets.Books;

/// <summary>
///     Pages of a book as the client sends them (0x66): the lines a player wrote on them, or a request for a page
///     (a line count of 0xFFFF).
/// </summary>
/// <remarks>
///     ModernUO's <c>ContentChange</c>: serial, page count, then for each page its number, its line count and its
///     lines in UTF-8, each ended by a zero. A packet that is not that carries no page at all, and nothing is
///     allocated by the counts it claims.
/// </remarks>
[PacketHandler(0x66, PacketSizing.Variable, MinimumLength = HeaderLength, Description = "Book pages request or edit")]
public sealed class BookPagesRequestPacket : BasePacket<BookPagesRequestPacket>, IIncomingPacket<BookPagesRequestPacket>
{
    private const int HeaderLength = 7;
    private const ushort PageRequest = 0xFFFF;

    public override int Length { get; }

    public Serial Book { get; }

    /// <summary>
    ///     The pages the packet carries; none when it is not well formed.
    /// </summary>
    public IReadOnlyList<BookPageEdit> Pages { get; }

    private BookPagesRequestPacket(int length, Serial book, IReadOnlyList<BookPageEdit> pages)
    {
        Length = length;
        Book = book;
        Pages = pages;
    }

    public static bool TryParse(ReadOnlySpan<byte> data, [NotNullWhen(true)] out BookPagesRequestPacket? packet)
    {
        packet = null;

        if (!HasValidHeader(data))
        {
            return false;
        }

        var reader = new PacketReader(data[3..]);

        if (!reader.TryReadSerial(out var book))
        {
            return false;
        }

        packet = new(data.Length, book, ReadPages(data[HeaderLength..]));

        return true;
    }

    private static IReadOnlyList<BookPageEdit> ReadPages(ReadOnlySpan<byte> data)
    {
        var position = 0;

        if (!TryReadUInt16(data, ref position, out var count) || count > BookPagination.MaxPages)
        {
            return [];
        }

        var pages = new List<BookPageEdit>();

        for (var index = 0; index < count; index++)
        {
            if (!TryReadUInt16(data, ref position, out var number) || !TryReadUInt16(data, ref position, out var lineCount))
            {
                return [];
            }

            if (lineCount == PageRequest)
            {
                pages.Add(new(number, null));

                continue;
            }

            if (lineCount > BookPagination.LinesPerPage)
            {
                return [];
            }

            var lines = new string[lineCount];

            for (var line = 0; line < lineCount; line++)
            {
                if (!BookPacketText.TryReadZeroEnded(data, ref position, out lines[line]))
                {
                    return [];
                }
            }

            pages.Add(new(number, lines));
        }

        return pages;
    }

    private static bool TryReadUInt16(ReadOnlySpan<byte> data, ref int position, out ushort value)
    {
        value = 0;

        if (position + sizeof(ushort) > data.Length)
        {
            return false;
        }

        value = BinaryPrimitives.ReadUInt16BigEndian(data[position..]);
        position += sizeof(ushort);

        return true;
    }
}
