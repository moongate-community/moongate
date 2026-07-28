using Moongate.Network.Packets.Incoming;
using Moongate.Network.Packets.Outgoing;
using Moongate.Network.Types;
using Moongate.Server.Abstractions.Data;
using Moongate.Server.Abstractions.Interfaces.Items;
using Moongate.Server.Abstractions.Interfaces.Network;

namespace Moongate.Server.Handlers;

/// <summary>
/// Handles pick up item (0x07): lifts the requested item onto the client's cursor, or refuses with
/// 0x27 and a reason. An unanswered request leaves the client's cursor stuck, so every path replies.
/// </summary>
public sealed class PickUpItemHandler : IPacketHandler<PickUpItemPacket>, IPacketHandlerRegistration
{
    private readonly IDragDropService _dragDrop;

    public PickUpItemHandler(IDragDropService dragDrop)
    {
        _dragDrop = dragDrop;
    }

    public void Handle(PickUpItemPacket packet, in PacketContext context)
    {
        if (context.Session.Character is not { } character)
        {
            context.Session.Send(new LiftRejectPacket(LiftRejectReasonType.Inspecific));

            return;
        }

        var decision = _dragDrop.Lift(
            character,
            packet.Serial,
            packet.Amount,
            context.Session.HeldItemId,
            out var heldId,
            out var origin
        );

        if (!decision.Accepted)
        {
            context.Session.Send(new LiftRejectPacket(decision.Reason));

            return;
        }

        context.Session.Hold(heldId, origin!);
    }

    public void Register(INetworkService network)
        => network.RegisterHandler(this);
}
