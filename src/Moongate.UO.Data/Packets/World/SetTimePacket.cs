using Moongate.Core.Server.Packets;
using Moongate.Core.Spans;

namespace Moongate.UO.Data.Packets.World;

public class SetTimePacket : BaseUoPacket
{
    public DateTime Time { get; set; }

    public SetTimePacket(DateTime time) : this()
    {
        Time = time;
    }

    public SetTimePacket() : base(0x5B)
    {
        Time = DateTime.Now;
    }

    public override ReadOnlyMemory<byte> Write(SpanWriter writer)
    {
        writer.Write(OpCode);
        writer.Write((byte)Time.Hour);
        writer.Write((byte)Time.Minute);
        writer.Write((byte)Time.Second);
        return writer.ToArray();
    }
}
