using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Network.Spans;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;

namespace Moongate.Network.Packets.Outgoing.Entity;

[PacketHandler(0x23, PacketSizing.Fixed, Length = 26, Description = "Dragging Of Item")]
/// <summary>
/// Represents Dragging Of Item packet (0x23).
/// This packet drives the drag animation only; it does not move item state.
/// </summary>
public class DraggingOfItemPacket : BaseGameNetworkPacket
{
    public ushort ItemId { get; set; }

    public byte Unknown { get; set; }

    public ushort Hue { get; set; }

    public ushort StackCount { get; set; } = 1;

    public Serial SourceId { get; set; }

    public Point3D SourceLocation { get; set; }

    public Serial TargetId { get; set; }

    public Point3D TargetLocation { get; set; }

    public DraggingOfItemPacket()
        : base(0x23, 26) { }

    public DraggingOfItemPacket(
        ushort itemId,
        ushort hue,
        ushort stackCount,
        Serial sourceId,
        Point3D sourceLocation,
        Serial targetId,
        Point3D targetLocation
    ) : this()
    {
        ItemId = itemId;
        Hue = hue;
        StackCount = stackCount;
        SourceId = sourceId;
        SourceLocation = sourceLocation;
        TargetId = targetId;
        TargetLocation = targetLocation;
    }

    public override void Write(ref SpanWriter writer)
    {
        writer.Write(OpCode);
        writer.Write(ItemId);
        writer.Write(Unknown);
        writer.Write(Hue);
        writer.Write(StackCount);
        writer.Write((uint)SourceId);
        writer.Write((short)SourceLocation.X);
        writer.Write((short)SourceLocation.Y);
        writer.Write((sbyte)SourceLocation.Z);
        writer.Write((uint)TargetId);
        writer.Write((short)TargetLocation.X);
        writer.Write((short)TargetLocation.Y);
        writer.Write((sbyte)TargetLocation.Z);
    }

    protected override bool ParsePayload(ref SpanReader reader)
    {
        if (reader.Remaining != 25)
        {
            return false;
        }

        ItemId = reader.ReadUInt16();
        Unknown = reader.ReadByte();
        Hue = reader.ReadUInt16();
        StackCount = reader.ReadUInt16();
        SourceId = (Serial)reader.ReadUInt32();
        var sourceX = reader.ReadInt16();
        var sourceY = reader.ReadInt16();
        var sourceZ = reader.ReadSByte();
        TargetId = (Serial)reader.ReadUInt32();
        var targetX = reader.ReadInt16();
        var targetY = reader.ReadInt16();
        var targetZ = reader.ReadSByte();

        SourceLocation = new(sourceX, sourceY, sourceZ);
        TargetLocation = new(targetX, targetY, targetZ);

        return reader.Remaining == 0;
    }
}
