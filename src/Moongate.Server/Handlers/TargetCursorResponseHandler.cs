using Moongate.Network.Packets.Incoming;
using Moongate.Server.Abstractions.Data;
using Moongate.Server.Abstractions.Interfaces.Network;
using Moongate.Server.Abstractions.Interfaces.World;

namespace Moongate.Server.Handlers;

/// <summary>
/// Hands a target answer (0x6C inbound) to the service, which is the only thing that knows what it
/// asked for and can tell a real answer from one to a cursor that no longer exists.
/// </summary>
public sealed class TargetCursorResponseHandler
    : IPacketHandler<TargetCursorResponsePacket>, IPacketHandlerRegistration
{
    private readonly IPlayerTargetService _targets;

    public TargetCursorResponseHandler(IPlayerTargetService targets)
    {
        _targets = targets;
    }

    public void Handle(TargetCursorResponsePacket packet, in PacketContext context)
    {
        // The verdict is the service's business: it logs a mismatch and drops it.
        _targets.Handle(context.Session, packet);
    }

    public void Register(INetworkService network)
        => network.RegisterHandler(this);
}
