using System.Text;
using Moongate.Core.Primitives;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Server.Ultima.Packets.Books;

/// <summary>
///     Opens the client's book on its cover (0xD4): the title, the author, how many pages follow and whether the
///     player may write in it.
/// </summary>
/// <remarks>
///     ModernUO's <c>SendBookCover</c>: serial, flag 1, writable, page count, then title and author in UTF-8, each
///     with the length of its bytes and of the zero that ends it.
/// </remarks>
[PacketHandler(0xD4, PacketSizing.Variable, MinimumLength = HeaderLength, Description = "Book header")]
public sealed class BookHeaderPacket : BasePacket<BookHeaderPacket>, IOutgoingPacket
{
    /// <summary>
    ///     The bytes of a title the client's field holds.
    /// </summary>
    public const int TitleBytes = 60;

    /// <summary>
    ///     The bytes of an author the client's field holds.
    /// </summary>
    public const int AuthorBytes = 30;

    private const int HeaderLength = 17;
    private const byte FlagOn = 0x01;

    private readonly byte[] _title;
    private readonly byte[] _author;

    public override int Length => HeaderLength + _title.Length + _author.Length;

    public Serial Book { get; }

    public int PageCount { get; }

    public bool Writable { get; }

    public BookHeaderPacket(Serial book, int pageCount, string title, string author, bool writable = false)
    {
        Book = book;
        PageCount = pageCount;
        Writable = writable;
        _title = Cut(title, TitleBytes);
        _author = Cut(author, AuthorBytes);
    }

    public void Write(ref PacketWriter writer)
    {
        writer.EnsureCapacity(Length);
        writer.WriteByte(OpCode);
        writer.WriteUInt16BigEndian((ushort)Length);
        writer.WriteSerial(Book);
        writer.WriteByte(FlagOn);
        writer.WriteByte(Writable ? (byte)1 : (byte)0);
        writer.WriteUInt16BigEndian((ushort)PageCount);
        writer.WriteUInt16BigEndian((ushort)(_title.Length + 1));
        writer.WriteBytes(_title);
        writer.WriteByte(0);
        writer.WriteUInt16BigEndian((ushort)(_author.Length + 1));
        writer.WriteBytes(_author);
        writer.WriteByte(0);
    }

    /// <summary>
    ///     Cuts <paramref name="text" /> to what <paramref name="limit" /> bytes of UTF-8 hold, before the character
    ///     that would pass them.
    /// </summary>
    public static string Fit(string text, int limit)
    {
        ArgumentNullException.ThrowIfNull(text);

        var length = 0;
        var characters = 0;

        foreach (var rune in text.EnumerateRunes())
        {
            if (length + rune.Utf8SequenceLength > limit)
            {
                break;
            }

            length += rune.Utf8SequenceLength;
            characters += rune.Utf16SequenceLength;
        }

        return text[..characters];
    }

    private static byte[] Cut(string text, int limit)
    {
        return Encoding.UTF8.GetBytes(Fit(text, limit));
    }
}
