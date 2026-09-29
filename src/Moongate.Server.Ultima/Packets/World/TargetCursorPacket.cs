using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Server.Ultima.Types.Targeting;

namespace Moongate.Server.Ultima.Packets.World;

/// <summary>
///     Shows the target cursor (0x6C) with an id the response must carry, or, with <see cref="TargetFlagsType.Cancel" />,
///     takes the client's cursor away.
/// </summary>
[PacketHandler(0x6C, PacketSizing.Fixed, Length = 19)]
public sealed class TargetCursorPacket : BaseFixedPacket<TargetCursorPacket>, IOutgoingPacket
{
    private const int Padding = 12;

    public TargetCursorType Cursor { get; }

    public int CursorId { get; }

    public TargetFlagsType Flags { get; }

    public TargetCursorPacket(TargetCursorType cursor, int cursorId, TargetFlagsType flags)
    {
        Cursor = cursor;
        CursorId = cursorId;
        Flags = flags;
    }

    /// <summary>
    ///     Gets the packet that takes the client's cursor away, as ModernUO and POL send it: id 0 and flags 3.
    /// </summary>
    public static TargetCursorPacket Cancel()
    {
        return new(TargetCursorType.Object, 0, TargetFlagsType.Cancel);
    }

    public void Write(ref PacketWriter writer)
    {
        writer.EnsureCapacity(Length);
        writer.WriteByte(OpCode);
        writer.WriteByte((byte)Cursor);
        writer.WriteUInt32BigEndian(unchecked((uint)CursorId));
        writer.WriteByte((byte)Flags);

        for (var i = 0; i < Padding; i++)
        {
            writer.WriteByte(0);
        }
    }
}
