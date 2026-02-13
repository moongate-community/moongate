using Moongate.Core.Server.Packets;
using Moongate.Core.Spans;

namespace Moongate.UO.Data.Packets.System;

public class PingPacket : BaseUoPacket
{
    public byte Sequence { get; set; }

    public PingPacket() : base(0x73) { }

    public override ReadOnlyMemory<byte> Write(SpanWriter writer)
    {
        writer.Write(OpCode);
        writer.Write(Sequence);

        return writer.ToArray();
    }

    protected override bool Read(SpanReader reader)
    {
        Sequence = reader.ReadByte();

        return true;
    }
}
