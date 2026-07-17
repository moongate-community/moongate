using DryIoc;
using Moongate.Server.Abstractions.Interfaces.Network;

namespace Moongate.Server.Extensions;

/// <summary>
/// Registers inbound packet handlers. Each handler is collected under
/// <see cref="IPacketHandlerRegistration" />, the typed list <see cref="System.Collections.Generic.IEnumerable{T}" />
/// that the network service resolves and wires to opcodes at startup.
/// </summary>
public static class PacketHandlerRegistrationExtensions
{
    /// <summary>
    /// Records a packet handler as a singleton <see cref="IPacketHandlerRegistration" /> so the network
    /// service picks it up alongside every other handler.
    /// </summary>
    public static IContainer RegisterPacketHandler<THandler>(this IContainer container)
        where THandler : class, IPacketHandlerRegistration
    {
        container.Register<IPacketHandlerRegistration, THandler>(Reuse.Singleton);

        return container;
    }
}
