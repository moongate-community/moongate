using Moongate.Core.Server.Packets;
using Moongate.Core.Spans;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Types;

namespace Moongate.UO.Data.Packets.Effects;

public class GraphicEffectPacket : BaseUoPacket
{
    public Serial Source { get; set; }

    public Serial Target { get; set; }

    public int EffectId { get; set; }

    public Point3D SourceLocation { get; set; } = Point3D.Zero;

    public Point3D TargetLocation { get; set; } = Point3D.Zero;

    public EffectDirectionType Direction { get; set; }

    public bool AdjustDirection { get; set; } = true;

    public bool ExplodeOnImpact { get; set; } = false;

    public int Speed { get; set; } = 1;

    public GraphicEffectPacket() : base(0x70)
    {
    }

    public override ReadOnlyMemory<byte> Write(SpanWriter writer)
    {
        writer.Write(OpCode);
        writer.Write((byte)Direction);

        writer.Write(Source.Value);
        writer.Write(Target.Value);

        writer.Write((short)EffectId);

        /**
         * BYTE[2] xLoc
           BYTE[2] yLoc
           BYTE zLoc
         */

        writer.Write((short)SourceLocation.X);
        writer.Write((short)SourceLocation.Y);
        writer.Write((sbyte)SourceLocation.Z);


        writer.Write((short)TargetLocation.X);
        writer.Write((short)TargetLocation.Y);
        writer.Write((sbyte)TargetLocation.Z);

        writer.Write((byte)Speed);
        writer.Write((short)0);

        writer.Write((byte)(AdjustDirection ? 1 : 0));
        writer.Write((byte)(ExplodeOnImpact ? 1 : 0));

        return writer.ToArray();
    }
}
