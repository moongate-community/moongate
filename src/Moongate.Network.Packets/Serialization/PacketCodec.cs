using System.Diagnostics.CodeAnalysis;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;

namespace Moongate.Network.Packets.Serialization;

public static class PacketCodec
{
    public static byte[] Encode(IOutgoingPacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);
        var length = packet.Length;

        if (length is < 1 or > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(packet),
                length,
                "Packet length must fit a non-empty UInt16 frame."
            );
        }

        var destination = new byte[length];
        var writer = new PacketWriter(destination);
        packet.Write(ref writer);

        if (writer.WrittenCount != length)
        {
            throw new InvalidOperationException("The packet did not write its declared complete length.");
        }

        return destination;
    }

    public static bool TryDecode<TPacket>(
        ReadOnlySpan<byte> data,
        [NotNullWhen(true)] out TPacket? packet
    )
        where TPacket : class, IIncomingPacket<TPacket>
        => TPacket.TryParse(data, out packet);
}
