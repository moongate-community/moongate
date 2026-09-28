using System.Diagnostics.CodeAnalysis;
using Moongate.Core.Primitives;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Server.Ultima.Packets.General;

/// <summary>
///     The player drops the item they hold (0x08, 15 bytes as clients from 6.0.1.7 send it): the item, the position,
///     a grid byte the server does not read, and the container or item it was dropped on (0xFFFFFFFF for the ground).
///     A position of -1, -1 means it was dropped on the container's icon.
/// </summary>
[PacketHandler(0x08, PacketSizing.Fixed, Length = 15, Description = "Drop request")]
public sealed class DropRequestPacket : BaseFixedPacket<DropRequestPacket>, IIncomingPacket<DropRequestPacket>
{
    public required Serial Item { get; init; }

    public required short X { get; init; }

    public required short Y { get; init; }

    public required sbyte Z { get; init; }

    public required Serial Destination { get; init; }

    public static bool TryParse(ReadOnlySpan<byte> data, [NotNullWhen(true)] out DropRequestPacket? packet)
    {
        packet = null;

        if (!HasValidHeader(data))
        {
            return false;
        }

        var reader = new PacketReader(data[1..]);

        if (!reader.TryReadUInt32BigEndian(out var item) ||
            !reader.TryReadUInt16BigEndian(out var x) ||
            !reader.TryReadUInt16BigEndian(out var y) ||
            !reader.TryReadByte(out var z) ||
            !reader.TryReadByte(out _) ||
            !reader.TryReadUInt32BigEndian(out var destination))
        {
            return false;
        }

        packet = new()
        {
            Item = new(item), X = unchecked((short)x), Y = unchecked((short)y), Z = unchecked((sbyte)z),
            Destination = new(destination)
        };

        return true;
    }
}
