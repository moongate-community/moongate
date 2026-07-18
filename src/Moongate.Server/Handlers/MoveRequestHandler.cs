using Moongate.Network.Packets.Incoming;
using Moongate.Server.Abstractions.Data;
using Moongate.Server.Abstractions.Interfaces.Network;
using Moongate.Server.Abstractions.Interfaces.World;

namespace Moongate.Server.Handlers;

/// <summary>
/// Handles player movement (0x02): validates and applies the step or turn via
/// <see cref="IMovementService" />, which replies to the mover and broadcasts to nearby players.
/// <c>FastwalkKey</c> is parsed by <see cref="MoveRequestPacket" /> but intentionally unused — rate
/// limiting is sequence- and timing-based, not the legacy fastwalk-key scheme.
/// </summary>
public sealed class MoveRequestHandler : IPacketHandler<MoveRequestPacket>, IPacketHandlerRegistration
{
    private readonly IMovementService _movement;

    public MoveRequestHandler(IMovementService movement)
    {
        _movement = movement;
    }

    public void Handle(MoveRequestPacket packet, in PacketContext context)
        => _movement.TryMove(context.Session, packet.Direction, packet.Sequence);

    public void Register(INetworkService network)
        => network.RegisterHandler(this);
}
