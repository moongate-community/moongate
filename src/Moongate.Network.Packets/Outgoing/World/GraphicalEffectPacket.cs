using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Network.Spans;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Types;

namespace Moongate.Network.Packets.Outgoing.World;

[PacketHandler(0x70, PacketSizing.Fixed, Length = 28, Description = "Graphical Effect")]
public class GraphicalEffectPacket : BaseGameNetworkPacket
{
    public EffectDirectionType DirectionType { get; set; }

    public Serial SourceId { get; set; }

    public Serial TargetId { get; set; }

    public ushort ItemId { get; set; }

    public Point3D SourceLocation { get; set; }

    public Point3D TargetLocation { get; set; }

    public byte Speed { get; set; }

    public byte Duration { get; set; }

    public ushort Unknown2 { get; set; }

    public bool AdjustDirectionDuringAnimation { get; set; } = true;

    public bool ExplodeOnImpact { get; set; }

    public GraphicalEffectPacket()
        : base(0x70, 28) { }

    public GraphicalEffectPacket(
        EffectDirectionType directionType,
        Serial sourceId,
        Serial targetId,
        ushort itemId,
        Point3D sourceLocation,
        Point3D targetLocation,
        byte speed,
        byte duration,
        ushort unknown2 = 0,
        bool adjustDirectionDuringAnimation = true,
        bool explodeOnImpact = false
    ) : this()
    {
        DirectionType = directionType;
        SourceId = sourceId;
        TargetId = targetId;
        ItemId = itemId;
        SourceLocation = sourceLocation;
        TargetLocation = targetLocation;
        Speed = speed;
        Duration = duration;
        Unknown2 = unknown2;
        AdjustDirectionDuringAnimation = adjustDirectionDuringAnimation;
        ExplodeOnImpact = explodeOnImpact;
    }

    public override void Write(ref SpanWriter writer)
    {
        writer.Write(OpCode);
        writer.Write((byte)DirectionType);
        writer.Write((uint)SourceId);
        writer.Write((uint)TargetId);
        writer.Write(ItemId);
        writer.Write((short)SourceLocation.X);
        writer.Write((short)SourceLocation.Y);
        writer.Write((sbyte)SourceLocation.Z);
        writer.Write((short)TargetLocation.X);
        writer.Write((short)TargetLocation.Y);
        writer.Write((sbyte)TargetLocation.Z);
        writer.Write(Speed);
        writer.Write(Duration);
        writer.Write(Unknown2);
        writer.Write(AdjustDirectionDuringAnimation);
        writer.Write(ExplodeOnImpact);
    }

    protected override bool ParsePayload(ref SpanReader reader)
    {
        if (reader.Remaining != 27)
        {
            return false;
        }

        DirectionType = (EffectDirectionType)reader.ReadByte();
        SourceId = (Serial)reader.ReadUInt32();
        TargetId = (Serial)reader.ReadUInt32();
        ItemId = reader.ReadUInt16();
        var sourceX = reader.ReadInt16();
        var sourceY = reader.ReadInt16();
        var sourceZ = reader.ReadSByte();
        var targetX = reader.ReadInt16();
        var targetY = reader.ReadInt16();
        var targetZ = reader.ReadSByte();
        Speed = reader.ReadByte();
        Duration = reader.ReadByte();
        Unknown2 = reader.ReadUInt16();
        AdjustDirectionDuringAnimation = reader.ReadBoolean();
        ExplodeOnImpact = reader.ReadBoolean();

        SourceLocation = new(sourceX, sourceY, sourceZ);
        TargetLocation = new(targetX, targetY, targetZ);

        return reader.Remaining == 0;
    }
}
