using Moongate.Network.Attributes;
using Moongate.Network.Interfaces;
using Moongate.Network.Types;
using SquidStd.Network.Spans;

namespace Moongate.Network.Packets.Outgoing;

/// <summary>
/// Character delete result (0x85): why a deletion was refused. 2 bytes fixed. Only sent on refusal —
/// a successful deletion is reported by sending the updated character list instead.
/// </summary>
[PacketDocumentation(PacketFamilyType.Characters)]
public readonly record struct CharacterDeleteResultPacket(DeleteResultType Reason) : IOutgoingPacket
{
    public const byte PacketId = 0x85;

    public void Write(ref SpanWriter writer)
    {
        writer.Write(PacketId);
        writer.Write((byte)Reason);
    }
}
