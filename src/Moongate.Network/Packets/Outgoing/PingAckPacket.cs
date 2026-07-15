using Moongate.Network.Interfaces;
using SquidStd.Network.Spans;

namespace Moongate.Network.Packets.Outgoing;

/// <summary>
/// Ping acknowledgement (0x73): echoes the client's keep-alive sequence byte straight back. 2 bytes fixed.
/// </summary>
public readonly record struct PingAckPacket(byte Sequence) : IOutgoingPacket
{
    public const byte PacketId = 0x73;

    public void Write(ref SpanWriter writer)
    {
        writer.Write(PacketId);
        writer.Write(Sequence);
    }
}
