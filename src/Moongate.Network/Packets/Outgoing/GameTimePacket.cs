using Moongate.Network.Attributes;
using Moongate.Network.Interfaces;
using Moongate.Network.Types;
using SquidStd.Network.Spans;

namespace Moongate.Network.Packets.Outgoing;

/// <summary>
/// Game time (0x5B): the in-world clock shown to the client. 4 bytes fixed.
/// </summary>
[PacketDocumentation(PacketFamilyType.WorldState)]
public readonly record struct GameTimePacket(byte Hour, byte Minute, byte Second) : IOutgoingPacket
{
    public const byte PacketId = 0x5B;

    public void Write(ref SpanWriter writer)
    {
        writer.Write(PacketId);
        writer.Write(Hour);
        writer.Write(Minute);
        writer.Write(Second);
    }
}
