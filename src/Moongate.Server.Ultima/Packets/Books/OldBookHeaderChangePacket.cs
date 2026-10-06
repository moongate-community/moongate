using System.Diagnostics.CodeAnalysis;
using System.Text;
using Moongate.Core.Primitives;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Server.Ultima.Packets.Books;

/// <summary>
///     The title and author a player wrote on a book, as the older clients send them (0x93).
/// </summary>
/// <remarks>
///     ModernUO's <c>OldHeaderChange</c>: serial, four bytes of flags and page count, the title in 60 bytes and the
///     author in 30, Latin-1 and zero filled.
/// </remarks>
[PacketHandler(0x93, PacketSizing.Fixed, Length = TotalLength, Description = "Book header change (old)")]
public sealed class OldBookHeaderChangePacket
    : BaseFixedPacket<OldBookHeaderChangePacket>, IIncomingPacket<OldBookHeaderChangePacket>
{
    private const int TotalLength = 99;
    private const int TitleOffset = 9;
    private const int TitleLength = 60;
    private const int AuthorLength = 30;

    public Serial Book { get; }

    public string Title { get; }

    public string Author { get; }

    private OldBookHeaderChangePacket(Serial book, string title, string author)
    {
        Book = book;
        Title = title;
        Author = author;
    }

    public static bool TryParse(ReadOnlySpan<byte> data, [NotNullWhen(true)] out OldBookHeaderChangePacket? packet)
    {
        packet = null;

        if (!HasValidHeader(data))
        {
            return false;
        }

        var reader = new PacketReader(data[1..]);

        if (!reader.TryReadSerial(out var book))
        {
            return false;
        }

        packet = new(
            book,
            Field(data.Slice(TitleOffset, TitleLength)),
            Field(data.Slice(TitleOffset + TitleLength, AuthorLength))
        );

        return true;
    }

    private static string Field(ReadOnlySpan<byte> field)
    {
        var zero = field.IndexOf((byte)0);

        return Encoding.Latin1.GetString(zero < 0 ? field : field[..zero]);
    }
}
