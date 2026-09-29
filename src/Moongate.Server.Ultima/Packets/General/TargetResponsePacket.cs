using System.Diagnostics.CodeAnalysis;
using Moongate.Core.Primitives;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Server.Ultima.Types.Targeting;

namespace Moongate.Server.Ultima.Packets.General;

/// <summary>
///     The client's answer to a target cursor (0x6C): what was clicked, or x and y -1 with no serial when the player
///     cancelled.
/// </summary>
[PacketHandler(0x6C, PacketSizing.Fixed, Length = 19, Description = "Target response")]
public sealed class TargetResponsePacket : BaseFixedPacket<TargetResponsePacket>, IIncomingPacket<TargetResponsePacket>
{
    public required TargetCursorType Cursor { get; init; }

    public required int CursorId { get; init; }

    public required TargetFlagsType Flags { get; init; }

    public required Serial Serial { get; init; }

    public required short X { get; init; }

    public required short Y { get; init; }

    public required sbyte Z { get; init; }

    public required ushort Graphic { get; init; }

    /// <summary>
    ///     Gets whether the player cancelled the cursor, as ModernUO reads it: x and y -1 and no serial.
    /// </summary>
    public bool IsCancel => X == -1 && Y == -1 && Serial.Value == 0;

    public static bool TryParse(ReadOnlySpan<byte> data, [NotNullWhen(true)] out TargetResponsePacket? packet)
    {
        packet = null;

        if (!HasValidHeader(data))
        {
            return false;
        }

        var reader = new PacketReader(data[1..]);

        if (!reader.TryReadByte(out var cursor) ||
            !reader.TryReadUInt32BigEndian(out var cursorId) ||
            !reader.TryReadByte(out var flags) ||
            !reader.TryReadUInt32BigEndian(out var serial) ||
            !reader.TryReadUInt16BigEndian(out var x) ||
            !reader.TryReadUInt16BigEndian(out var y) ||
            !reader.TryReadByte(out _) ||
            !reader.TryReadByte(out var z) ||
            !reader.TryReadUInt16BigEndian(out var graphic))
        {
            return false;
        }

        packet = new()
        {
            Cursor = (TargetCursorType)cursor, CursorId = unchecked((int)cursorId), Flags = (TargetFlagsType)flags,
            Serial = new(serial), X = unchecked((short)x), Y = unchecked((short)y), Z = unchecked((sbyte)z),
            Graphic = graphic
        };

        return true;
    }
}
