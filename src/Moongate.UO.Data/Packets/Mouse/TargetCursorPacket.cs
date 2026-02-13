using Moongate.Core.Server.Packets;
using Moongate.Core.Spans;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Types;

namespace Moongate.UO.Data.Packets.Mouse;

public class TargetCursorPacket : BaseUoPacket
{
    public Serial CursorId { get; set; }
    public CursorSelectionType SelectionType { get; set; }
    public CursorType CursorType { get; set; }

    public Serial ClickedSerial { get; set; }
    public Point3D ClickedPoint { get; set; }
    public int TileId { get; set; }

    public TargetCursorPacket() : base(0x6C) { }

    public TargetCursorPacket(CursorSelectionType selectionType, CursorType cursorType, Serial cursorId) : this()
    {
        SelectionType = selectionType;
        CursorType = cursorType;
        CursorId = cursorId;
    }

    public override ReadOnlyMemory<byte> Write(SpanWriter writer)
    {
        writer.Write(OpCode);
        writer.Write((byte)SelectionType);
        writer.Write(CursorId.Value);
        writer.Write((byte)CursorType);

        //The following are always sent but are only valid if sent by client
        writer.Write(0);
        writer.Write((short)0);
        writer.Write((short)0);
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((short)0);

        return writer.ToArray();
    }

    protected override bool Read(SpanReader reader)
    {
        SelectionType = (CursorSelectionType)reader.ReadByte();
        CursorId = (Serial)reader.ReadUInt32();
        CursorType = (CursorType)reader.ReadByte();
        ClickedSerial = (Serial)reader.ReadUInt32();
        var x = reader.ReadUInt16();
        var y = reader.ReadUInt16();
        reader.ReadByte(); // This is always 0x00
        var z = reader.ReadByte();
        ClickedPoint = new(x, y, z);
        TileId = reader.ReadUInt16();

        return true;
    }
}
