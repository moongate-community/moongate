using DryIoc;
using Moongate.Network.Packets.Registry;
using Moongate.Server.Core.Data.Packets;

namespace Moongate.Server.Bootstrap.Internal;

/// <summary>
///     Builds the packet registry the server frames and decodes with: the built-in packets plus every incoming packet a
///     plugin added with <c>RegisterIncomingPacket</c>. It is built on first use, when every plugin has registered,
///     and frozen.
/// </summary>
internal static class PacketRegistryFactory
{
    public static void Register(Container container)
    {
        if (container.IsRegistered<PacketRegistry>())
        {
            return;
        }

        container.RegisterDelegate(Create, Reuse.Singleton);
    }

    public static PacketRegistry Create(IResolverContext resolver)
    {
        var registry = new PacketRegistry();
        PacketTable.Register(registry);

        var registrations = resolver.Resolve<List<IncomingPacketRegistration>>(IfUnresolved.ReturnDefault) ?? [];

        foreach (var registration in registrations)
        {
            registration.Register(registry);
        }

        registry.Freeze();

        return registry;
    }
}
