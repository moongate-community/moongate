using SquidStd.Network.Spans;

namespace Moongate.Network.Interfaces;

/// <summary>A packet that can serialize itself, id-first, into a span writer.</summary>
public interface IOutgoingPacket
{
    void Write(ref SpanWriter writer);
}
