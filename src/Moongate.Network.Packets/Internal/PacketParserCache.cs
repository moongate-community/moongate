using System.Reflection;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Network.Packets.Internal;

/// <summary>
/// Binds an incoming parser once per packet type; outgoing-only packets have no parser.
/// Decoding uses the cached delegate and static interface dispatch.
/// </summary>
internal static class PacketParserCache<TPacket>
    where TPacket : class, IPacket
{
    public static PacketParser? Parser { get; } = Create();

    private static PacketParser? Create()
    {
        if ((PacketMetadataCache<TPacket>.Descriptor.Direction & PacketDirection.Incoming) == 0)
        {
            return null;
        }

        var factory = typeof(PacketParserCache<TPacket>)
                      .GetMethod(nameof(CreateIncomingParser), BindingFlags.NonPublic | BindingFlags.Static)!
                      .MakeGenericMethod(typeof(TPacket))
                      .CreateDelegate<Func<PacketParser>>();

        return factory();
    }

    private static PacketParser CreateIncomingParser<TIncoming>()
        where TIncoming : class, IIncomingPacket<TIncoming>
        => PacketParserAdapter<TIncoming>.TryParse;
}
