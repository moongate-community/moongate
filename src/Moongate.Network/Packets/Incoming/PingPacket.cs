using Moongate.Network.Attributes;
using Moongate.Network.Interfaces;
using Moongate.Network.Types;
using SquidStd.Network.Spans;

namespace Moongate.Network.Packets.Incoming;

/// <summary>
/// Ping / keep-alive (0x73): the client sends this periodically with a rolling sequence byte and
/// expects the server to echo it straight back, or it eventually drops the connection. 2 bytes fixed.
/// </summary>
[PacketDocumentation(PacketFamilyType.InteractionKeepalive)]
public readonly record struct PingPacket(byte Sequence) : IIncomingPacket<PingPacket>
{
    public static byte PacketId => 0x73;

    public static PingPacket Read(ref SpanReader reader)
    {
        reader.ReadByte(); // packet id

        return new(reader.ReadByte());
    }
}
