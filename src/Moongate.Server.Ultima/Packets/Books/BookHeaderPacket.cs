using System.Text;
using Moongate.Core.Primitives;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Server.Ultima.Packets.Books;

/// <summary>
///     Opens the client's book on its cover (0xD4): the title, the author and how many pages follow. The book is
///     read only.
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
    private const byte ReadOnly = 0x00;

    private readonly byte[] _title;
    private readonly byte[] _author;

    public override int Length => HeaderLength + _title.Length + _author.Length;

    public Serial Book { get; }

    public int PageCount { get; }

    public BookHeaderPacket(Serial book, int pageCount, string title, string author)
    {
        Book = book;
        PageCount = pageCount;
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
        writer.WriteByte(ReadOnly);
        writer.WriteUInt16BigEndian((ushort)PageCount);
        writer.WriteUInt16BigEndian((ushort)(_title.Length + 1));
        writer.WriteBytes(_title);
        writer.WriteByte(0);
        writer.WriteUInt16BigEndian((ushort)(_author.Length + 1));
        writer.WriteBytes(_author);
        writer.WriteByte(0);
    }

    // The text in UTF-8, cut before the character that would pass the limit.
    private static byte[] Cut(string text, int limit)
    {
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

        return Encoding.UTF8.GetBytes(text[..characters]);
    }
}
