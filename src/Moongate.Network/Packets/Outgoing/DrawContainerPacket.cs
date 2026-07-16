using Moongate.Core.Primitives;
using Moongate.Network.Interfaces;
using SquidStd.Network.Spans;

namespace Moongate.Network.Packets.Outgoing;

/// <summary>
/// Draw container (0x24): opens the container's gump on the client. 9 bytes — the modern client expects
/// a trailing constant the older 7-byte form did not have. The contents follow in a separate container
/// content (0x3C) packet.
/// </summary>
public readonly record struct DrawContainerPacket(Serial Container, ushort GumpId) : IOutgoingPacket
{
    public const byte PacketId = 0x24;

    // What ModernUO sends for High Seas clients, which is every client we support.
    private const short ModernTrailer = 0x7D;

    public void Write(ref SpanWriter writer)
    {
        writer.Write(PacketId);
        writer.Write(Container);
        writer.Write(GumpId);
        writer.Write(ModernTrailer);
    }
}
