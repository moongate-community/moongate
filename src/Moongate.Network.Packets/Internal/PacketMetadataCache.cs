using Moongate.Network.Packets.Data.Packets;
using Moongate.Network.Packets.Interfaces;

namespace Moongate.Network.Packets.Internal;

internal static class PacketMetadataCache<TPacket>
    where TPacket : class, IPacket
{
    public static PacketDescriptor Descriptor { get; } = PacketMetadata.Create(typeof(TPacket));
}
