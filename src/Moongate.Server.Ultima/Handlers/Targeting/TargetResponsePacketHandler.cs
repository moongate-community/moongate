using Moongate.Core.Geometry;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Packets;
using Moongate.Server.Ultima.Data.Targeting;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Packets.General;
using Moongate.Server.Ultima.Types.Targeting;
using Serilog;

namespace Moongate.Server.Ultima.Handlers.Targeting;

/// <summary>
///     Turns the client's target response (0x6C) into a <see cref="TargetResult" /> for the player's pending target: a
///     live item or mobile, the ground at the map's height, the top of a static that is really there, or a cancel. A
///     response for another cursor, or with no target pending, is ignored.
/// </summary>
public sealed class TargetResponsePacketHandler : IPacketHandler<TargetResponsePacket>
{
    private readonly ILogger _logger = Log.ForContext<TargetResponsePacketHandler>();
    private readonly ITargetService _targets;
    private readonly IMobileService _mobiles;
    private readonly IItemService _items;
    private readonly IMapService _maps;
    private readonly ITileDataService _tiles;
    private readonly IMovementService _movement;

    public TargetResponsePacketHandler(
        ITargetService targets,
        IMobileService mobiles,
        IItemService items,
        IMapService maps,
        ITileDataService tiles,
        IMovementService movement
    )
    {
        _targets = targets;
        _mobiles = mobiles;
        _items = items;
        _maps = maps;
        _tiles = tiles;
        _movement = movement;
    }

    public void Handle(GameSession session, TargetResponsePacket packet)
    {
        if (!_targets.TryComplete(session, packet.CursorId, Resolve(session, packet)))
        {
            _logger.Debug(
                "Session {SessionId} answered cursor {CursorId}, which is not pending",
                session.SessionId,
                packet.CursorId
            );
        }
    }

    private TargetResult Resolve(GameSession session, TargetResponsePacket packet)
    {
        if (packet.IsCancel || !_mobiles.TryGet(session.CharacterId, out var mobile))
        {
            return TargetResult.Canceled(TargetCancelType.Canceled);
        }

        if (packet.Serial.Value != 0)
        {
            return _items.TryGet(packet.Serial, out _) || _mobiles.IsInWorld(packet.Serial)
                       ? TargetResult.ForObject(packet.Serial)
                       : TargetResult.Canceled(TargetCancelType.Canceled);
        }

        var map = mobile.Map;

        if (!_maps.Contains(map, packet.X, packet.Y))
        {
            return TargetResult.Canceled(TargetCancelType.Canceled);
        }

        if (packet.Graphic == 0)
        {
            // As ModernUO's LandTarget: the client's height is not trusted.
            var z = _movement.GetAverageZ(map, packet.X, packet.Y);

            return TargetResult.ForLocation(map, new Point3D(packet.X, packet.Y, z));
        }

        // As ModernUO's StaticTarget: the static clicked must be there, at the height clicked (its base, or its top
        // from High Seas clients), and the spot is its top (half a bridge's height).
        foreach (var tile in _maps.GetStatics(map, packet.X, packet.Y))
        {
            if (tile.Id != packet.Graphic)
            {
                continue;
            }

            var item = _tiles.GetItem(tile.Id);

            if (tile.Z == packet.Z || tile.Z + item.Height == packet.Z)
            {
                return TargetResult.ForLocation(map, new Point3D(packet.X, packet.Y, tile.Z + item.StandHeight));
            }
        }

        return TargetResult.Canceled(TargetCancelType.Canceled);
    }
}
