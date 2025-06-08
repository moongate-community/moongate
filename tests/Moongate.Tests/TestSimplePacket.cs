using Moongate.Core.Server.Packets;
using Moongate.Core.Spans;

namespace Moongate.Tests;

public class TestSimplePacket : BaseUoPacket
{

    public int Number { get; set; }
    public TestSimplePacket() : base(0x01)
    {
    }


    public override ReadOnlyMemory<byte> Write(SpanWriter writer)
    {
        writer.Write(OpCode);
        writer.Write(Number);
        return writer.ToArray();
    }

    protected override bool Read(SpanReader reader)
    {
        var opCode = reader.ReadByte();
        Number = reader.ReadInt32();

        return true;
    }
}
