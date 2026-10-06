using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using Moongate.Core.Primitives;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Server.Ultima.Packets.Books.Internal;

namespace Moongate.Server.Ultima.Packets.Books;

/// <summary>
///     The title and author a player wrote on a book (0xD4).
/// </summary>
/// <remarks>
///     ModernUO's <c>HeaderChange</c>: serial, four bytes of flags and page count, then the title and the author in
///     UTF-8, each with the length of its bytes. A packet that is not that carries no text.
/// </remarks>
[PacketHandler(0xD4, PacketSizing.Variable, MinimumLength = HeaderLength, Description = "Book header change")]
public sealed class BookHeaderChangePacket : BasePacket<BookHeaderChangePacket>, IIncomingPacket<BookHeaderChangePacket>
{
    private const int HeaderLength = 7;
    private const int Skipped = 4;

    public override int Length { get; }

    public Serial Book { get; }

    /// <summary>
    ///     The title written; null when the packet is not well formed.
    /// </summary>
    public string? Title { get; }

    /// <summary>
    ///     The author written; null when the packet is not well formed.
    /// </summary>
    public string? Author { get; }

    private BookHeaderChangePacket(int length, Serial book, string? title, string? author)
    {
        Length = length;
        Book = book;
        Title = title;
        Author = author;
    }

    public static bool TryParse(ReadOnlySpan<byte> data, [NotNullWhen(true)] out BookHeaderChangePacket? packet)
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

        var body = data[HeaderLength..];
        var position = Skipped;

        packet = TryReadText(body, ref position, out var title) && TryReadText(body, ref position, out var author)
            ? new(data.Length, book, title, author)
            : new(data.Length, book, null, null);

        return true;
    }

    private static bool TryReadText(ReadOnlySpan<byte> data, ref int position, out string text)
    {
        text = "";

        if (position < 0 || position + sizeof(ushort) > data.Length)
        {
            return false;
        }

        var length = BinaryPrimitives.ReadUInt16BigEndian(data[position..]);
        position += sizeof(ushort);

        return BookPacketText.TryReadField(data, ref position, length, out text);
    }
}
