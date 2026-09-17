using DryIoc;
using Moongate.Network.Packets.Interfaces;
using Moongate.Server.Core.Interfaces.Packets;
using Moongate.Server.Core.Packets;

namespace Moongate.Server.Core.Extensions;

/// <summary>Registers container-owned singleton packet handlers without resolving dependencies.</summary>
public static class PacketHandlerContainerExtensions
{
    /// <summary>Registers one typed handler and its deferred binder before dispatcher startup.</summary>
    public static Container RegisterPacketHandler<TPacket, THandler>(this Container container)
        where TPacket : class, IIncomingPacket<TPacket>
        where THandler : class, IPacketHandler<TPacket>
    {
        if (!container.IsRegistered<PacketHandlerRegistry>())
        {
            container.RegisterInstance(new PacketHandlerRegistry());
        }

        container.Resolve<PacketHandlerRegistry>().Register<TPacket, THandler>(container);
        return container;
    }
}
