using Moongate.Network.Attributes;
using Moongate.Network.Interfaces;
using Moongate.Network.Types;
using SquidStd.Network.Spans;

namespace Moongate.Network.Packets.Outgoing;

/// <summary>
/// Drop item approved (0x29): the drop the client asked for went through. Opcode only, 1 byte — the
/// client already knows what it dropped and where. A refused drop gets 0x27 instead.
/// </summary>
[PacketDocumentation(PacketFamilyType.ItemsContainers, Length = 1)]
public readonly record struct DropItemApprovedPacket : IOutgoingPacket
{
    public const byte PacketId = 0x29;

    public void Write(ref SpanWriter writer)
        => writer.Write(PacketId);
}
