using Moongate.Network.Packets.Incoming;
using Moongate.Network.Packets.Outgoing;
using Moongate.Network.Types;
using Moongate.Server.Abstractions.Data;
using Moongate.Server.Abstractions.Interfaces.Items;
using Moongate.Server.Abstractions.Interfaces.Network;

namespace Moongate.Server.Handlers;

/// <summary>
/// Handles drop-wear (0x13): the held item was dropped onto a paperdoll. A refusal sends 0x27 and
/// bounces the item back to where it was lifted from, otherwise it would stay attached to nothing —
/// the same shape as a refused drop.
/// </summary>
public sealed class DropWearItemHandler : IPacketHandler<DropWearItemPacket>, IPacketHandlerRegistration
{
    private readonly IDragDropService _dragDrop;

    public DropWearItemHandler(IDragDropService dragDrop)
    {
        _dragDrop = dragDrop;
    }

    public void Handle(DropWearItemPacket packet, in PacketContext context)
    {
        var session = context.Session;

        if (session.Character is not { } character)
        {
            session.Send(new LiftRejectPacket(LiftRejectReasonType.Inspecific));

            return;
        }

        // packet.Layer is deliberately not passed on: the service takes the layer from the item, so a
        // client claiming a dagger goes on the boots layer is simply not asked.
        var decision = _dragDrop.Wear(character, session.HeldItemId, packet.Mobile);

        if (!decision.Accepted)
        {
            _dragDrop.Bounce(character, session.HeldItemId, session.HeldItemOrigin);
            session.ClearHold();
            session.Send(new LiftRejectPacket(decision.Reason));

            return;
        }

        // No approval packet: the WornItemPacket the service broadcasts is what tells the client the
        // item landed, and it reaches the wearer along with everyone else.
        session.ClearHold();
    }

    public void Register(INetworkService network)
        => network.RegisterHandler(this);
}
