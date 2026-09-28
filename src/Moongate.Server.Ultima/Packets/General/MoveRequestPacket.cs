using System.Diagnostics.CodeAnalysis;
using Moongate.Core.Types.Geometry;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Server.Ultima.Packets.General;

/// <summary>
///     A step the client asks to take (0x02, 7 bytes): the direction with the running bit, the step sequence and the
///     fastwalk key, which the server does not check.
/// </summary>
[PacketHandler(0x02, PacketSizing.Fixed, Length = 7, Description = "Move request")]
public sealed class MoveRequestPacket : BaseFixedPacket<MoveRequestPacket>, IIncomingPacket<MoveRequestPacket>
{
    private const byte RunningBit = 0x80;
    private const byte DirectionMask = 0x07;

    /// <summary>
    ///     Gets the direction, without the running bit.
    /// </summary>
    public required DirectionType Direction { get; init; }

    public required bool Running { get; init; }

    /// <summary>
    ///     Gets the step number: 0 after login or a refused step, then 1 to 255 and round again from 1.
    /// </summary>
    public required byte Sequence { get; init; }

    public required uint FastWalkKey { get; init; }

    public static bool TryParse(ReadOnlySpan<byte> data, [NotNullWhen(true)] out MoveRequestPacket? packet)
    {
        packet = null;

        if (!HasValidHeader(data))
        {
            return false;
        }

        var reader = new PacketReader(data[1..]);

        if (!reader.TryReadByte(out var direction) ||
            !reader.TryReadByte(out var sequence) ||
            !reader.TryReadUInt32BigEndian(out var key))
        {
            return false;
        }

        packet = new()
        {
            Direction = (DirectionType)(direction & DirectionMask),
            Running = (direction & RunningBit) != 0,
            Sequence = sequence,
            FastWalkKey = key
        };

        return true;
    }
}
