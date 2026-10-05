using System.Diagnostics.CodeAnalysis;
using Moongate.Core.Primitives;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Server.Ultima.Packets.Books;

/// <summary>
///     The title and author a player wrote on a book (0xD4). Books are read only: it is recognised, so the stream goes on, and
///     nothing is done with it.
/// </summary>
[PacketHandler(0xD4, PacketSizing.Variable, MinimumLength = HeaderLength, Description = "Book header change")]
public sealed class BookHeaderChangePacket : BasePacket<BookHeaderChangePacket>, IIncomingPacket<BookHeaderChangePacket>
{
    private const int HeaderLength = 7;

    public override int Length { get; }

    public Serial Book { get; }

    private BookHeaderChangePacket(int length, Serial book)
    {
        Length = length;
        Book = book;
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

        packet = new(data.Length, book);

        return true;
    }
}
