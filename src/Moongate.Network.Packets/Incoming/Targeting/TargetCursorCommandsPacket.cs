using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Network.Packets.Types.Targeting;
using Moongate.Network.Spans;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;

namespace Moongate.Network.Packets.Incoming.Targeting;

[PacketHandler(0x6C, PacketSizing.Fixed, Length = 19, Description = "Target Cursor Commands")]

/// <summary>
/// Represents target cursor command packet (0x6C), sent by both server and client.
/// </summary>
public class TargetCursorCommandsPacket : BaseGameNetworkPacket
{
    public TargetCursorSelectionType CursorTarget { get; set; }

    public Serial CursorId { get; set; }

    public TargetCursorType CursorType { get; set; }

    public Serial ClickedOnId { get; set; }

    public Point3D Location { get; set; }

    public byte Unknown { get; set; }

    public ushort Graphic { get; set; }

    public TargetCursorCommandsPacket()
        : base(0x6C, 19) { }

    public TargetCursorCommandsPacket(TargetCursorSelectionType cursorTarget, Serial cursorId, TargetCursorType cursorType)
        : this()
    {
        CursorTarget = cursorTarget;
        CursorId = cursorId;
        CursorType = cursorType;
    }

    /// <summary>
    /// Creates a server-side cancel cursor command.
    /// </summary>
    public static TargetCursorCommandsPacket CreateCancelCurrentTarget()
        => new(TargetCursorSelectionType.SelectObject, (Serial)0u, TargetCursorType.CancelCurrentTargeting);

    public override void Write(ref SpanWriter writer)
    {
        writer.Write(OpCode);
        writer.Write((byte)CursorTarget);
        writer.Write((uint)CursorId);
        writer.Write((byte)CursorType);
        writer.Write((uint)ClickedOnId);
        writer.Write((ushort)Location.X);
        writer.Write((ushort)Location.Y);
        writer.Write(Unknown);
        writer.Write((byte)Location.Z);
        writer.Write(Graphic);
    }

    protected override bool ParsePayload(ref SpanReader reader)
    {
        if (reader.Remaining != 18)
        {
            return false;
        }

        CursorTarget = (TargetCursorSelectionType)reader.ReadByte();
        CursorId = (Serial)reader.ReadUInt32();
        CursorType = (TargetCursorType)reader.ReadByte();
        ClickedOnId = (Serial)reader.ReadUInt32();
        var x = reader.ReadUInt16();
        var y = reader.ReadUInt16();
        Unknown = reader.ReadByte();
        var z = unchecked((sbyte)reader.ReadByte());
        Graphic = reader.ReadUInt16();
        Location = new(x, y, z);

        return reader.Remaining == 0;
    }
}
