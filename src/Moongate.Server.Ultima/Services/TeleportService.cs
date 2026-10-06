using Moongate.Core.Geometry;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Movement;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Ultima.Primitives;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Teleports mobiles on their map or to another: the mover's own client gets the map change when there is one,
///     <see cref="IMobileService.MoveTo" /> moves the mobile, its client gets 0x20 as in ModernUO's
///     <c>SetLocation</c>, then <see cref="IWorldViewService" /> tells the players around.
/// </summary>
public sealed class TeleportService : ITeleportService
{
    private readonly IMobileService _mobiles;
    private readonly IWorldViewService _view;
    private readonly ISessionService _sessions;
    private readonly IPacketSendService _sender;
    private readonly ISectorService _sectors;
    private readonly IBankService _bank;

    public TeleportService(
        IMobileService mobiles,
        IWorldViewService view,
        ISessionService sessions,
        IPacketSendService sender,
        ISectorService sectors,
        IBankService bank
    )
    {
        _bank = bank;
        _sectors = sectors;
        _mobiles = mobiles;
        _view = view;
        _sessions = sessions;
        _sender = sender;
    }

    public bool Teleport(MobileEntity mobile, MapType map, Point3D location)
    {
        var oldMap = mobile.Map;
        var oldLocation = mobile.Location;
        var hasSession = _sessions.TryGetByCharacterId(mobile.Id, out var session);

        // Checked here too: the map change must leave before the move, and only for a move that happens.
        if (!_mobiles.IsInWorld(mobile.Id) || !_sectors.IsInside(map, location.X, location.Y))
        {
            return false;
        }

        // Before the move: the season, the light and the weather the move brings belong to the new map.
        if (hasSession && map != oldMap)
        {
            // Safe: hasSession is true, so TryGetByCharacterId set the session.
            _sender.TrySend(session!.SessionId, new MapChangePacket(map));
        }

        if (!_mobiles.MoveTo(mobile, map, location))
        {
            return false;
        }

        // The bank is open on a spot: a teleport leaves it, even one that ends where it began.
        _bank.Close(mobile);

        if (hasSession)
        {
            // The client starts its step sequence again when it gets 0x20; a step it sent before is refused, and the next
            // one is due at once, as ModernUO.
            // Safe: hasSession is true, so TryGetByCharacterId set the session.
            if (session!.Get(MovementSessionKeys.State) is { } state)
            {
                state.ExpectedSequence = 0;
                state.NextStepAt = 0;
            }

            _sender.TrySend(
                // Safe: hasSession is true, so TryGetByCharacterId set the session.
                session!.SessionId,
                new MobileUpdatePacket(
                    mobile.Id,
                    new Body((ushort)mobile.Body),
                    mobile.SkinHue,
                    _mobiles.GetFlags(mobile),
                    mobile.Location,
                    mobile.Direction
                )
            );
        }

        // After 0x20: the client must know where it stands before it is shown what is around.
        _view.Teleported(mobile, oldMap, oldLocation);

        return true;
    }
}
