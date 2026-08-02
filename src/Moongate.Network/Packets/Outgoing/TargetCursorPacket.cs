using Moongate.Network.Attributes;
using Moongate.Network.Interfaces;
using Moongate.Network.Types;
using Moongate.UO.Data.Types;
using SquidStd.Network.Spans;

namespace Moongate.Network.Packets.Outgoing;

/// <summary>
/// Target cursor (0x6C, outgoing): raises the client's targeting cursor, or takes it down when
/// <paramref name="CursorType" /> is <see cref="TargetCursorType.Cancel" />.
/// <para>
/// The same opcode carries the answer back, as <see cref="Incoming.TargetCursorResponsePacket" />.
/// They are two records rather than one because every other packet here has a single direction —
/// the interfaces differ and the docs generator files pages by them.
/// </para>
/// <para>
/// Only the selection type, the cursor id and the cursor type mean anything outbound; the fields
/// the client fills in on the way back are written as zero. 19 bytes fixed.
/// </para>
/// </summary>
[PacketDocumentation(PacketFamilyType.Targeting, Length = 19)]
public readonly record struct TargetCursorPacket(
    uint CursorId,
    TargetSelectionType Selection,
    TargetCursorType CursorType
) : IOutgoingPacket
{
    public const byte PacketId = 0x6C;

    public void Write(ref SpanWriter writer)
    {
        writer.Write(PacketId);
        writer.Write((byte)Selection);
        writer.Write(CursorId);
        writer.Write((byte)CursorType);
        writer.Write(0u);        // clicked serial, filled by the client
        writer.Write((ushort)0); // x
        writer.Write((ushort)0); // y
        writer.Write((byte)0);   // unused
        writer.Write((byte)0);   // z
        writer.Write((ushort)0); // graphic
    }
}
