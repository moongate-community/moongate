using Moongate.Core.Primitives;
using Moongate.Network.Packets.Incoming;
using Moongate.Server.Data;
using Moongate.Server.Data.Events;
using Moongate.Server.Interfaces.Network;
using SquidStd.Core.Interfaces.Events;

namespace Moongate.Server.Handlers;

/// <summary>
/// Handles double click (0x06): turns the click into a typed event —
/// <see cref="MobileDoubleClickEvent" /> for a mobile target, <see cref="ItemDoubleClickEvent" /> for
/// an item — and publishes it. It performs no lookup and sends nothing back; behavior belongs to
/// subscribers.
/// </summary>
public sealed class DoubleClickHandler : IPacketHandler<DoubleClickPacket>, IPacketHandlerRegistration
{
    private readonly IEventBus _eventBus;

    public DoubleClickHandler(IEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    public void Handle(DoubleClickPacket packet, in PacketContext context)
        => Publish(context.Session.SessionId, packet.Target, _eventBus);

    public static void Publish(long sessionId, Serial target, IEventBus eventBus)
    {
        if (target.IsMobile)
        {
            eventBus.Publish(new MobileDoubleClickEvent(sessionId, target));
        }
        else
        {
            eventBus.Publish(new ItemDoubleClickEvent(sessionId, target));
        }
    }

    public void Register(INetworkService network)
        => network.RegisterHandler(this);
}
