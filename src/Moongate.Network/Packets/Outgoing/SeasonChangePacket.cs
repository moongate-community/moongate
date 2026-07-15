using Moongate.Network.Interfaces;
using SquidStd.Network.Spans;

namespace Moongate.Network.Packets.Outgoing;

/// <summary>
/// Seasonal information (0xBC): sets the client's season and optionally plays the season-change
/// sound. 3 bytes fixed.
/// </summary>
public readonly record struct SeasonChangePacket(byte Season, bool PlaySound) : IOutgoingPacket
{
    public const byte PacketId = 0xBC;

    public void Write(ref SpanWriter writer)
    {
        writer.Write(PacketId);
        writer.Write(Season);
        writer.Write((byte)(PlaySound ? 1 : 0));
    }
}
