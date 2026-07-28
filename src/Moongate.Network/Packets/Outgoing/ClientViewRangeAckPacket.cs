using Moongate.Network.Attributes;
using Moongate.Network.Interfaces;
using Moongate.Network.Types;
using SquidStd.Network.Spans;

namespace Moongate.Network.Packets.Outgoing;

/// <summary>
/// Client view range acknowledgement (0xC8): reports the view range the server actually granted, so a
/// client that asked for more than the maximum learns what it got. 2 bytes fixed.
/// </summary>
[PacketDocumentation(PacketFamilyType.WorldState, Length = 2)]
public readonly record struct ClientViewRangeAckPacket(byte Range) : IOutgoingPacket
{
    public const byte PacketId = 0xC8;

    public void Write(ref SpanWriter writer)
    {
        writer.Write(PacketId);
        writer.Write(Range);
    }
}
