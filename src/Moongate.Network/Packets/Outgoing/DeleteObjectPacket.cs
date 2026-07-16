using Moongate.Core.Primitives;
using Moongate.Network.Interfaces;
using SquidStd.Network.Spans;

namespace Moongate.Network.Packets.Outgoing;

/// <summary>
/// Delete object (0x1D): the entity is gone — stop drawing it. 5 bytes fixed. Sent to everyone who can
/// see a mobile or item that was removed, deleted or moved out of view.
/// </summary>
public readonly record struct DeleteObjectPacket(Serial Serial) : IOutgoingPacket
{
    public const byte PacketId = 0x1D;

    public void Write(ref SpanWriter writer)
    {
        writer.Write(PacketId);
        writer.Write(Serial);
    }
}
