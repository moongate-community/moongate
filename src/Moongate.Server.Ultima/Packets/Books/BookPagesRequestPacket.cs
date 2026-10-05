using System.Diagnostics.CodeAnalysis;
using Moongate.Core.Primitives;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Server.Ultima.Packets.Books;

/// <summary>
///     A page the client asks for, or the lines a player wrote (0x66). Books are read only: it is recognised, so the stream goes on, and
///     nothing is done with it.
/// </summary>
[PacketHandler(0x66, PacketSizing.Variable, MinimumLength = HeaderLength, Description = "Book pages request or edit")]
public sealed class BookPagesRequestPacket : BasePacket<BookPagesRequestPacket>, IIncomingPacket<BookPagesRequestPacket>
{
    private const int HeaderLength = 7;

    public override int Length { get; }

    public Serial Book { get; }

    private BookPagesRequestPacket(int length, Serial book)
    {
        Length = length;
        Book = book;
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

        packet = new(data.Length, book);

        return true;
    }
}
