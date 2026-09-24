using Moongate.Network.Packets.Interfaces;

namespace Moongate.Network.Packets.Internal;

internal static class PacketParserAdapter<TPacket>
    where TPacket : class, IIncomingPacket<TPacket>
{
    public static bool TryParse(ReadOnlySpan<byte> data, out IPacket? packet)
    {
        if (TPacket.TryParse(data, out var parsed))
        {
            packet = parsed;

            return true;
        }

        packet = null;

        return false;
    }
}
