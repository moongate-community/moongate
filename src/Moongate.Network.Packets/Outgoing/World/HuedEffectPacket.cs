using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Network.Spans;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Types;

namespace Moongate.Network.Packets.Outgoing.World;

[PacketHandler(0xC0, PacketSizing.Fixed, Length = 36, Description = "Hued Effect")]
public class HuedEffectPacket : BaseGameNetworkPacket
{
    public EffectDirectionType DirectionType { get; set; }

    public Serial SourceId { get; set; }

    public Serial TargetId { get; set; }

    public ushort ItemId { get; set; }

    public Point3D SourceLocation { get; set; }

    public Point3D TargetLocation { get; set; }

    public byte Speed { get; set; }

    public byte Duration { get; set; }

    public byte Unknown1 { get; set; }

    public byte Unknown2 { get; set; }

    public bool FixedDirection { get; set; }

    public bool Explode { get; set; }

    public int Hue { get; set; }

    public int RenderMode { get; set; }

    public HuedEffectPacket()
        : base(0xC0, 36) { }

    public HuedEffectPacket(
        EffectDirectionType directionType,
        Serial sourceId,
        Serial targetId,
        ushort itemId,
        Point3D sourceLocation,
        Point3D targetLocation,
        byte speed,
        byte duration,
        bool fixedDirection,
        bool explode,
        int hue,
        int renderMode,
        byte unknown1 = 0,
        byte unknown2 = 0
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
        FixedDirection = fixedDirection;
        Explode = explode;
        Hue = hue;
        RenderMode = renderMode;
        Unknown1 = unknown1;
        Unknown2 = unknown2;
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
        writer.Write(Unknown1);
        writer.Write(Unknown2);
        writer.Write(FixedDirection);
        writer.Write(Explode);
        writer.Write(Hue);
        writer.Write(RenderMode);
    }

    protected override bool ParsePayload(ref SpanReader reader)
    {
        if (reader.Remaining != 35)
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
        Unknown1 = reader.ReadByte();
        Unknown2 = reader.ReadByte();
        FixedDirection = reader.ReadBoolean();
        Explode = reader.ReadBoolean();
        Hue = reader.ReadInt32();
        RenderMode = reader.ReadInt32();

        SourceLocation = new(sourceX, sourceY, sourceZ);
        TargetLocation = new(targetX, targetY, targetZ);

        return reader.Remaining == 0;
    }
}
