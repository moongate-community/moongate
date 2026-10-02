using Moongate.Core.Geometry;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Movement;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Ultima.Primitives;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Teleports mobiles on their map: <see cref="IMobileService.MoveTo" /> moves the mobile, the mover's own client gets
///     0x20 as in ModernUO's <c>SetLocation</c>, then <see cref="IWorldViewService" /> tells the players around.
/// </summary>
public sealed class TeleportService : ITeleportService
{
    private readonly IMobileService _mobiles;
    private readonly IWorldViewService _view;
    private readonly ISessionService _sessions;
    private readonly IPacketSendService _sender;

    public TeleportService(
        IMobileService mobiles,
        IWorldViewService view,
        ISessionService sessions,
        IPacketSendService sender
    )
    {
        _mobiles = mobiles;
        _view = view;
        _sessions = sessions;
        _sender = sender;
    }

    public bool Teleport(MobileEntity mobile, Point3D location)
    {
        var oldLocation = mobile.Location;

        if (!_mobiles.MoveTo(mobile, location))
        {
            return false;
        }

        if (_sessions.TryGetByCharacterId(mobile.Id, out var session))
        {
            // The client starts its step sequence again when it gets 0x20; a step it sent before is refused, and the next
            // one is due at once, as ModernUO.
            if (session.Get(MovementSessionKeys.State) is { } state)
            {
                state.ExpectedSequence = 0;
                state.NextStepAt = 0;
            }

            _sender.TrySend(
                session.SessionId,
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
        _view.Teleported(mobile, oldLocation);

        return true;
    }
}
