using Moongate.Core.Primitives;
using Moongate.Network.Interfaces;
using SquidStd.Network.Spans;

namespace Moongate.Network.Packets.Outgoing;

/// <summary>
/// Personal light level (0x4E): the light radiating around the given mobile. 6 bytes fixed.
/// </summary>
public readonly record struct PersonalLightLevelPacket(Serial Serial, byte Level) : IOutgoingPacket
{
    public const byte PacketId = 0x4E;

    public void Write(ref SpanWriter writer)
    {
        writer.Write(PacketId);
        writer.Write(Serial);
        writer.Write(Level);
    }
}
