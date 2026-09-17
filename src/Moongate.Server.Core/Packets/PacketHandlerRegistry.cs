using System.Collections.Frozen;
using DryIoc;
using Moongate.Network.Packets.Interfaces;
using Moongate.Server.Core.Data.Packets;
using Moongate.Server.Core.Interfaces.Packets;

namespace Moongate.Server.Core.Packets;

/// <summary>Collects typed handler metadata until dispatcher startup freezes registrations.</summary>
public sealed class PacketHandlerRegistry
{
    private readonly Lock _gate = new();
    private readonly Dictionary<Type, PacketHandlerRegistration> _registrations = new();
    private FrozenDictionary<Type, PacketHandlerRegistration>? _frozen;

    /// <summary>Gets an immutable snapshot of the registered packet types and deferred binders.</summary>
    public IReadOnlyDictionary<Type, PacketHandlerRegistration> Registrations
    {
        get
        {
            lock (_gate)
            {
                return _frozen ?? _registrations.ToFrozenDictionary();
            }
        }
    }

    /// <summary>Closes registration and returns stable, read-only metadata without resolving handlers.</summary>
    public IReadOnlyDictionary<Type, PacketHandlerRegistration> Freeze()
    {
        lock (_gate)
        {
            return _frozen ??= _registrations.ToFrozenDictionary();
        }
    }

    internal void Register<TPacket, THandler>(Container container)
        where TPacket : class, IIncomingPacket<TPacket>
        where THandler : class, IPacketHandler<TPacket>
    {
        lock (_gate)
        {
            if (_frozen is not null)
            {
                throw new InvalidOperationException("Packet handler registration is frozen.");
            }

            if (_registrations.ContainsKey(typeof(TPacket)))
            {
                throw new InvalidOperationException($"A handler for {typeof(TPacket).Name} is already registered.");
            }

            if (container.IsRegistered<THandler>(condition: factory => factory.Reuse != Reuse.Singleton))
            {
                throw new InvalidOperationException($"Packet handler {typeof(THandler).Name} must be registered as a singleton.");
            }

            if (!container.IsRegistered<THandler>())
            {
                container.Register<THandler>(Reuse.Singleton);
            }

            _registrations.Add(typeof(TPacket), new PacketHandlerRegistration(
                typeof(TPacket),
                typeof(THandler),
                resolver =>
                {
                    var handler = resolver.Resolve<THandler>();
                    return (session, packet) => handler.Handle(session, (TPacket)packet);
                }
            ));
        }
    }
}
