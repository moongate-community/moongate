using Moongate.Network.Attributes;
using Moongate.Network.Interfaces;
using Moongate.Network.Types;
using SquidStd.Network.Spans;

namespace Moongate.Network.Packets.Outgoing;

/// <summary>
/// Overall (world) light level (0x4F): 0 is full daylight, higher is darker. 2 bytes fixed.
/// </summary>
[PacketDocumentation(PacketFamilyType.WorldState)]
public readonly record struct OverallLightLevelPacket(byte Level) : IOutgoingPacket
{
    public const byte PacketId = 0x4F;

    public void Write(ref SpanWriter writer)
    {
        writer.Write(PacketId);
        writer.Write(Level);
    }
}
