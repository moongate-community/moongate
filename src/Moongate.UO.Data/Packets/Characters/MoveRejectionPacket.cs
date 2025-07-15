using Moongate.Core.Server.Packets;
using Moongate.Core.Spans;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Types;

namespace Moongate.UO.Data.Packets.Characters;

public class MoveRejectionPacket : BaseUoPacket
{

    public int Sequence { get; set; }
    public Point3D Location { get; set; }
    public DirectionType Direction { get; set; }


    public MoveRejectionPacket() : base(0x21)
    {
    }

    public MoveRejectionPacket(int sequence, Point3D location, DirectionType direction) : this()
    {
        Sequence = sequence;
        Location = location;
        Direction = direction;
    }

    public override ReadOnlyMemory<byte> Write(SpanWriter writer)
    {

        writer.Write(OpCode);
        writer.Write((byte)Sequence);
        writer.Write((short)Location.X);
        writer.Write((short)Location.Y);
        writer.Write((byte)Direction);
        writer.Write((byte)Location.Z);

        return writer.ToArray();
    }
}
