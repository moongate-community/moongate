using Moongate.Core.Server.Packets;
using Moongate.Core.Spans;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.UO.Data.Packets.Characters;

public class MoveAckPacket  :BaseUoPacket
{
    public UOMobileEntity Mobile { get; set; }

    public byte Sequence { get; set; }

    public MoveAckPacket() : base(0x22)
    {
    }

    public MoveAckPacket(UOMobileEntity mobile, byte sequence) : this()
    {
        Mobile = mobile;
        Sequence = sequence;
    }

    public override ReadOnlyMemory<byte> Write(SpanWriter writer)
    {
        writer.Write(OpCode);
        writer.Write(Sequence);
        writer.Write((byte)Mobile.Notoriety);
        return writer.ToArray();
    }
}
