using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Network.Attributes;
using Moongate.Network.Interfaces;
using Moongate.Network.Types;
using Moongate.UO.Data.Types;
using SquidStd.Network.Spans;

namespace Moongate.Network.Packets.Incoming;

/// <summary>
/// Target cursor response (0x6C, incoming): what the player clicked.
/// <para>
/// This record only reports. Whether the answer belongs to a cursor the server actually raised,
/// and whether "nothing picked" means the player cancelled, is decided by the service that owns
/// the pending request — the packet cannot tell.
/// </para>
/// </summary>
[PacketDocumentation(PacketFamilyType.Targeting, Length = 19)]
public readonly record struct TargetCursorResponsePacket(
    uint CursorId,
    TargetSelectionType Selection,
    Serial Clicked,
    Point3D Location,
    ushort Graphic
) : IIncomingPacket<TargetCursorResponsePacket>
{
    public static byte PacketId => 0x6C;

    public static TargetCursorResponsePacket Read(ref SpanReader reader)
    {
        reader.ReadByte(); // packet id

        var selection = (TargetSelectionType)reader.ReadByte();
        var cursorId = reader.ReadUInt32();

        reader.ReadByte(); // cursor type, echoed back and not needed

        var clicked = new Serial(reader.ReadUInt32());
        var x = reader.ReadUInt16();
        var y = reader.ReadUInt16();

        reader.ReadByte(); // unused

        var z = (sbyte)reader.ReadByte();
        var graphic = reader.ReadUInt16();

        return new(cursorId, selection, clicked, new(x, y, z), graphic);
    }
}
