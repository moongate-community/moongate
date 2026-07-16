using Moongate.Network.Attributes;
using Moongate.Network.Interfaces;
using Moongate.Network.Types;
using SquidStd.Network.Spans;

namespace Moongate.Network.Packets.Outgoing;

/// <summary>
/// Reject move item request (0x27): the lift the client asked for is refused, and why. 2 bytes fixed.
/// There is no matching "lift approved" — a successful lift is confirmed by the packets that follow it.
/// </summary>
[PacketDocumentation(PacketFamilyType.ItemsContainers)]
public readonly record struct LiftRejectPacket(LiftRejectReasonType Reason) : IOutgoingPacket
{
    public const byte PacketId = 0x27;

    public void Write(ref SpanWriter writer)
    {
        writer.Write(PacketId);
        writer.Write((byte)Reason);
    }
}
