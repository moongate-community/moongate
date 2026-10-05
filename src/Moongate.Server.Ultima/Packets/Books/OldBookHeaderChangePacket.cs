using System.Diagnostics.CodeAnalysis;
using Moongate.Core.Primitives;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Server.Ultima.Packets.Books;

/// <summary>
///     The title and author a player wrote on a book, as the older clients send them (0x93). Books are read only:
///     it is recognised, so the stream goes on, and nothing is done with it.
/// </summary>
[PacketHandler(0x93, PacketSizing.Fixed, Length = TotalLength, Description = "Book header change (old)")]
public sealed class OldBookHeaderChangePacket : BaseFixedPacket<OldBookHeaderChangePacket>, IIncomingPacket<OldBookHeaderChangePacket>
{
    private const int TotalLength = 99;

    public Serial Book { get; }

    private OldBookHeaderChangePacket(Serial book)
    {
        Book = book;
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

        packet = new(book);

        return true;
    }
}
