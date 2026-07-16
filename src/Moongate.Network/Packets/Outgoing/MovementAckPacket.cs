using Moongate.Network.Attributes;
using Moongate.Network.Interfaces;
using Moongate.Network.Types;
using Moongate.UO.Data.Types;
using SquidStd.Network.Spans;

namespace Moongate.Network.Packets.Outgoing;

/// <summary>Movement ack (0x22): confirms the step with the client's sequence number.</summary>
[PacketDocumentation(PacketFamilyType.Movement, Length = 3)]
public readonly record struct MovementAckPacket(byte Sequence, NotorietyType Notoriety) : IOutgoingPacket
{
    public const byte PacketId = 0x22;

    public void Write(ref SpanWriter writer)
    {
        writer.Write(PacketId);
        writer.Write(Sequence);
        writer.Write((byte)Notoriety);
    }
}
