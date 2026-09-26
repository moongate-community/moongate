using DryIoc;
using Moongate.Core.Extensions.Container;
using Moongate.Network.Packets.Interfaces;
using Moongate.Server.Core.Data.Packets;
using Moongate.Server.Core.Interfaces.Packets;
using Moongate.Server.Core.Packets;

namespace Moongate.Server.Core.Extensions;

/// <summary>
///     Registers container-owned singleton packet handlers without resolving dependencies.
/// </summary>
public static class PacketHandlerContainerExtensions
{
    /// <summary>
    ///     Registers one typed handler and its deferred binder before dispatcher startup.
    /// </summary>
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

    /// <summary>
    ///     Registers an asynchronous typed handler and its deferred binder.
    /// </summary>
    public static Container RegisterAsyncPacketHandler<TPacket, THandler>(this Container container)
        where TPacket : class, IIncomingPacket<TPacket>
        where THandler : class, IAsyncPacketHandler<TPacket>
    {
        if (!container.IsRegistered<PacketHandlerRegistry>())
        {
            container.RegisterInstance(new PacketHandlerRegistry());
        }

        container.Resolve<PacketHandlerRegistry>().RegisterAsync<TPacket, THandler>(container);

        return container;
    }

    /// <summary>
    ///     Adds an incoming packet type to the server's packet registry, so the server can frame and decode it. Use it
    ///     for packets a plugin defines; the built-in login and network packets are always registered.
    /// </summary>
    public static Container RegisterIncomingPacket<TPacket>(this Container container)
        where TPacket : class, IIncomingPacket<TPacket>
    {
        container.AddToRegisterTypedList(
            new IncomingPacketRegistration(typeof(TPacket), registry => registry.RegisterIncoming<TPacket>())
        );

        return container;
    }
}
