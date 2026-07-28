using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Network.Packets.Incoming;
using Moongate.Network.Packets.Outgoing;
using Moongate.Network.Types;
using Moongate.Server.Abstractions.Data;
using Moongate.Server.Abstractions.Interfaces.Items;
using Moongate.Server.Abstractions.Interfaces.Network;

namespace Moongate.Server.Handlers;

/// <summary>
/// Handles drop item (0x08): places the held item on the ground or into a container, answering 0x29
/// on success. A refusal sends 0x27 and bounces the item back to where it was lifted from, otherwise
/// it would stay attached to nothing.
/// </summary>
public sealed class DropItemHandler : IPacketHandler<DropItemPacket>, IPacketHandlerRegistration
{
    private readonly IDragDropService _dragDrop;

    public DropItemHandler(IDragDropService dragDrop)
    {
        _dragDrop = dragDrop;
    }

    public void Handle(DropItemPacket packet, in PacketContext context)
    {
        var session = context.Session;

        if (session.Character is not { } character)
        {
            session.Send(new LiftRejectPacket(LiftRejectReasonType.Inspecific));

            return;
        }

        // The wire uses all-ones for "on the ground"; the service uses Serial.Zero.
        var container = packet.Container.Value == DropItemPacket.GroundContainer
            ? Serial.Zero
            : packet.Container;

        var decision = _dragDrop.Drop(
            character,
            session.HeldItemId,
            container,
            new Point3D(packet.X, packet.Y, packet.Z),
            new Point2D(packet.X, packet.Y)
        );

        if (!decision.Accepted)
        {
            _dragDrop.Bounce(character, session.HeldItemId, session.HeldItemOrigin);
            session.ClearHold();
            session.Send(new LiftRejectPacket(decision.Reason));

            return;
        }

        session.ClearHold();
        session.Send(new DropItemApprovedPacket());
    }

    public void Register(INetworkService network)
        => network.RegisterHandler(this);
}
