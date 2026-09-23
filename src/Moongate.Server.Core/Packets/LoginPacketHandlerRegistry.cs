using System.Collections.Frozen;
using DryIoc;
using Moongate.Network.Packets.Interfaces;
using Moongate.Server.Core.Data.Packets;
using Moongate.Server.Core.Interfaces.Packets;

namespace Moongate.Server.Core.Packets;

/// <summary>Collects login-only handlers before the listener starts.</summary>
public sealed class LoginPacketHandlerRegistry
{
    private readonly Lock _gate = new();
    private readonly Dictionary<Type, LoginPacketHandlerRegistration> _registrations = new();
    private FrozenDictionary<Type, LoginPacketHandlerRegistration>? _frozen;

    public IReadOnlyDictionary<Type, LoginPacketHandlerRegistration> Freeze()
    {
        lock (_gate)
        {
            return _frozen ??= _registrations.ToFrozenDictionary();
        }
    }

    internal void Register<TPacket, THandler>(Container container)
        where TPacket : class, IIncomingPacket<TPacket>
        where THandler : class, ILoginPacketHandler<TPacket>
    {
        lock (_gate)
        {
            if (_frozen is not null)
            {
                throw new InvalidOperationException("Login packet registration is frozen.");
            }

            if (_registrations.ContainsKey(typeof(TPacket)))
            {
                throw new InvalidOperationException($"A login handler for {typeof(TPacket).Name} is already registered.");
            }

            if (!container.IsRegistered<THandler>())
            {
                container.Register<THandler>(Reuse.Singleton);
            }

            _registrations.Add(typeof(TPacket), new(typeof(TPacket), resolver =>
            {
                var handler = resolver.Resolve<THandler>();
                return (session, packet, token) => handler.HandleAsync(session, (TPacket)packet, token);
            }));
        }
    }
}
