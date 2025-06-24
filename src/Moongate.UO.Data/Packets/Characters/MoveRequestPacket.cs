using Moongate.Core.Server.Packets;
using Moongate.Core.Spans;
using Moongate.UO.Data.Types;

namespace Moongate.UO.Data.Packets.Characters;

public class MoveRequestPacket : BaseUoPacket
{
    public DirectionType Direction { get; set; }

    public int Sequence { get; set; }
    public int FastKey { get; set; }

    public MoveRequestPacket() : base(0x02)
    {
    }

    protected override bool Read(SpanReader reader)
    {
        Direction = (DirectionType)reader.ReadByte();
        Sequence = reader.ReadByte();
        FastKey = reader.ReadInt32();

        return true;
    }
}
