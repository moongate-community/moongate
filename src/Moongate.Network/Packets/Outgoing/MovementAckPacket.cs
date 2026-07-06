using SquidStd.Network.Spans;

namespace Moongate.Network.Packets.Outgoing;

/// <summary>Movement ack (0x22): confirms the step with the client's sequence number.</summary>
public readonly record struct MovementAckPacket(byte Sequence, byte Notoriety)
{
    public const byte PacketId = 0x22;

    public void Write(ref SpanWriter writer)
    {
        writer.Write(PacketId);
        writer.Write(Sequence);
        writer.Write(Notoriety);
    }
}
