using System.Diagnostics.CodeAnalysis;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Server.Ultima.Types.MapItems;

namespace Moongate.Server.Ultima.Packets.General;

/// <summary>
///     A player's change to the course of an open map item (0x56): add, insert, move or remove a pin, clear the course,
///     or toggle whether it may be changed. The point is in pixels of the drawing.
/// </summary>
[PacketHandler(0x56, PacketSizing.Fixed, Length = 11, Description = "Map command")]
public sealed class MapCommandRequestPacket : BaseFixedPacket<MapCommandRequestPacket>, IIncomingPacket<MapCommandRequestPacket>
{
    public required uint Serial { get; init; }

    public required MapCommandType Command { get; init; }

    /// <summary>
    ///     Gets the index of the pin the command is about, for insert, change and remove.
    /// </summary>
    public required int Number { get; init; }

    public required int X { get; init; }

    public required int Y { get; init; }

    public static bool TryParse(ReadOnlySpan<byte> data, [NotNullWhen(true)] out MapCommandRequestPacket? packet)
    {
        packet = null;

        if (!HasValidHeader(data))
        {
            return false;
        }

        var reader = new PacketReader(data[1..]);

        if (!reader.TryReadUInt32BigEndian(out var serial) ||
            !reader.TryReadByte(out var command) ||
            !reader.TryReadByte(out var number) ||
            !reader.TryReadUInt16BigEndian(out var x) ||
            !reader.TryReadUInt16BigEndian(out var y))
        {
            return false;
        }

        packet = new()
        {
            Serial = serial,
            Command = (MapCommandType)command,
            Number = number,
            X = unchecked((short)x),
            Y = unchecked((short)y)
        };

        return true;
    }
}
