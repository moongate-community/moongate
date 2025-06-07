using Moongate.Core.Server.Interfaces.Packets;
using Moongate.Core.Spans;

namespace Moongate.Core.Server.Packets;

public abstract class BaseUoPacket : IUoNetworkPacket
{
    public byte OpCode { get; }

    protected BaseUoPacket(byte opCode)
    {
        OpCode = opCode;
    }

    public bool Read(ReadOnlyMemory<byte> data)
    {
        if (data.IsEmpty)
        {
            return false;
        }

        var reader = new SpanReader(data.Span);
        return reader.ReadByte() == OpCode && Read(reader);
    }

    protected virtual bool Read(SpanReader reader)
    {
        return false;
    }

    public ReadOnlyMemory<byte> Write()
    {
        var writer = new SpanWriter();
        writer.Write(OpCode);
        Write(writer);
        return writer.ToArray();
    }

    protected virtual void Write(SpanWriter writer)
    {
    }
}
