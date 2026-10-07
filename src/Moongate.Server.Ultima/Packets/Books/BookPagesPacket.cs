using System.Text;
using Moongate.Core.Primitives;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Server.Ultima.Packets.Books;

/// <summary>
///     The pages of a book (0x66), all of them, sent after its header.
/// </summary>
/// <remarks>
///     ModernUO's <c>SendBookContent</c>: serial, page count, then each page with its number from 1, its line count
///     and its lines in UTF-8, each ended by a zero.
/// </remarks>
[PacketHandler(0x66, PacketSizing.Variable, MinimumLength = HeaderLength, Description = "Book pages")]
public sealed class BookPagesPacket : BasePacket<BookPagesPacket>, IOutgoingPacket
{
    private const int HeaderLength = 9;
    private const int PageHeaderLength = 4;

    private readonly byte[][][] _pages;

    public override int Length { get; }

    public Serial Book { get; }

    public int PageCount => _pages.Length;

    public BookPagesPacket(Serial book, IReadOnlyList<IReadOnlyList<string>> pages)
    {
        ArgumentNullException.ThrowIfNull(pages);

        Book = book;
        _pages = pages.Select(page => page.Select(Encoding.UTF8.GetBytes).ToArray()).ToArray();
        var length = HeaderLength + _pages.Sum(page => PageHeaderLength + page.Sum(line => line.Length + 1));

        if (length > ushort.MaxValue)
        {
            throw new ArgumentException("The pages do not fit one packet.", nameof(pages));
        }

        Length = length;
    }

    public void Write(ref PacketWriter writer)
    {
        writer.EnsureCapacity(Length);
        writer.WriteByte(OpCode);
        writer.WriteUInt16BigEndian((ushort)Length);
        writer.WriteSerial(Book);
        writer.WriteUInt16BigEndian((ushort)_pages.Length);

        for (var index = 0; index < _pages.Length; index++)
        {
            writer.WriteUInt16BigEndian((ushort)(index + 1));
            writer.WriteUInt16BigEndian((ushort)_pages[index].Length);

            foreach (var line in _pages[index])
            {
                writer.WriteBytes(line);
                writer.WriteByte(0);
            }
        }
    }
}
