using Moongate.Core.Primitives;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Data.Session;
using Moongate.Server.Abstractions.Data.World;
using Moongate.Server.Abstractions.Types.World;

namespace Moongate.Server.Abstractions.Interfaces.World;

/// <summary>
/// Remembers what each client has been told exists, and sends the difference when that stops
/// matching the world.
/// <para>
/// Two operations rather than one, and the split is about cost. <see cref="Refresh" /> recomputes a
/// whole view and is for the moments when all of it may have changed: entering the world, the player
/// themselves moving, and the periodic reconciliation. <see cref="UpdateFor(PlayerSession,
/// MobileEntity)" /> applies a single object's change and is what every other mobile's step uses —
/// recomputing every nearby session's whole view on every NPC step would be many full-radius scans
/// per tick for one standing player.
/// </para>
/// </summary>
public interface IVisibilityService
{
    /// <summary>
    /// Recomputes what this session can see, sends what entered and what left, and returns the
    /// difference. An empty delta means no packets were sent, which is what the reconciliation sweep
    /// should see almost every time.
    /// </summary>
    VisibilityDelta Refresh(PlayerSession session);

    /// <summary>Applies one mobile's change to one session: draw it, undraw it, or move it.</summary>
    VisibilityChangeType UpdateFor(PlayerSession session, MobileEntity mobile);

    /// <summary>Applies one item's change to one session.</summary>
    VisibilityChangeType UpdateFor(PlayerSession session, ItemEntity item);

    /// <summary>
    /// Tells every session that knew this serial to forget it, and returns how many did. For things
    /// that left the world rather than the view: range says nothing about something that is gone.
    /// </summary>
    int Undraw(IEnumerable<PlayerSession> sessions, Serial serial);

    /// <summary>Drops everything this session was told, on logout.</summary>
    void Forget(PlayerSession session);

    /// <summary>What this client currently believes exists, for tests and admin views.</summary>
    IReadOnlySet<Serial> KnownTo(PlayerSession session);
}
