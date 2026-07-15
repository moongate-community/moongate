using Moongate.Network.Interfaces;
using SquidStd.Network.Spans;

namespace Moongate.Network.Packets.Outgoing;

/// <summary>
/// War mode (0x72): toggles the client's combat stance. 5 bytes fixed.
/// </summary>
public readonly record struct WarModePacket(bool WarMode) : IOutgoingPacket
{
    public const byte PacketId = 0x72;

    public void Write(ref SpanWriter writer)
    {
        writer.Write(PacketId);
        writer.Write((byte)(WarMode ? 1 : 0));
        writer.Write((byte)0x00);
        writer.Write((byte)0x32);
        writer.Write((byte)0x00);
    }
}
