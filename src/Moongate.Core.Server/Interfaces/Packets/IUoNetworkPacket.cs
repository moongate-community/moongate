using Moongate.Core.Spans;

namespace Moongate.Core.Server.Interfaces.Packets;

public interface IUoNetworkPacket
{
    byte OpCode { get; }

    bool Read(ReadOnlyMemory<byte> data);

    ReadOnlyMemory<byte> Write(SpanWriter writer);

}
