using Moongate.Core.Extensions;
using Moongate.Core.Interfaces;
using Moongate.Core.Primitives;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Data.Events;
using Moongate.Server.Abstractions.Interfaces.Mobiles;
using Moongate.Server.Abstractions.Interfaces.World;
using SquidStd.Core.Interfaces.Events;
using SquidStd.Persistence.Abstractions.Interfaces.Persistence;
using Moongate.Network.Packets.Outgoing;
using Moongate.Server.Abstractions.Interfaces.Accounts;
using Moongate.Server.Services.World;

namespace Moongate.Server.Services.Mobiles;

/// <summary>
/// Default <see cref="IMobileService" />. Carries the loop-affinity guard on every mutation, the way
/// <c>ItemService</c> does — which is what <c>MobileModule</c>'s own comment asked for and could not
/// have while it wrote the store directly.
/// </summary>
public sealed class MobileService : IMobileService
{
    private readonly IEntityStore<MobileEntity, Serial> _mobiles;
    private readonly ISpatialIndexService _spatial;
    private readonly IEventBus _eventBus;
    private readonly ISessionManager? _sessions;
    private readonly ILoopAffinity? _loopAffinity;

    public MobileService(
        IPersistenceService persistenceService,
        ISpatialIndexService spatial,
        IEventBus eventBus,
        ISessionManager? sessions = null,
        ILoopAffinity? loopAffinity = null
    )
    {
        _mobiles = persistenceService.GetStore<MobileEntity, Serial>();
        _spatial = spatial;
        _eventBus = eventBus;
        _sessions = sessions;
        _loopAffinity = loopAffinity;
    }

    /// <summary>
    /// Tells a teleported player's own client where it now is.
    /// <para>
    /// A walk never needs this: the client moves itself and the server only corrects it when it is
    /// wrong. A teleport is the opposite — the client has no idea it happened, so without this the
    /// character stays exactly where it was on screen while the server serves it a world it cannot
    /// see. That is what made the go command look like it did nothing at all.
    /// </para>
    /// <para>
    /// An NPC has no session, and this is silent for it.
    /// </para>
    /// </summary>
    private void Redraw(MobileEntity mobile)
    {
        if (_sessions?.All.FirstOrDefault(session => session.Character?.Id == mobile.Id) is not { } session)
        {
            return;
        }

        // The client treats a draw-game-player as a resync and restarts its move sequence at 0, so the
        // server has to forget what it was expecting. Without this the very first step after any
        // teleport arrives as seq=0 against an expected 87, is rejected, and the rejection sends the
        // player straight back where they came from -- a teleport that holds for exactly one second.
        session.SetLastMove(null, session.LastMoveAt);
        session.ClearMovementQueue();

        session.Send(
            new MobileUpdatePacket(
                mobile.Id,
                (ushort)mobile.Body,
                mobile.SkinHue,
                MobileDrawing.BuildFlags(mobile),
                (ushort)mobile.Position.X,
                (ushort)mobile.Position.Y,
                (sbyte)mobile.Position.Z,
                mobile.Direction
            )
        );
    }

    public bool Teleport(Serial mobile, int x, int y, int z)
    {
        _loopAffinity?.AssertOnLoop("mobile.teleport");

        // The store clones on every read, so a played character exists twice: the session's live
        // instance, which movement reads and writes, and whatever GetById hands back. Teleporting the
        // store's clone lasts exactly one step — the next walk evaluates from the session's instance,
        // still standing where it was, and saves that over the jump. The session's copy is the one
        // that has to move.
        var entity = _sessions?.All.FirstOrDefault(session => session.Character?.Id == mobile)?.Character ??
                     _mobiles.GetById(mobile);

        if (entity is null)
        {
            return false;
        }

        var fromMapId = entity.MapId;
        var fromPosition = entity.Position;

        entity.Position = new(x, y, z);
        _mobiles.UpsertAsync(entity).WaitSync();
        _spatial.AddOrUpdate(entity);

        // Only a real move is worth telling the world about: the brain router and every other
        // subscriber would otherwise wake for nothing.
        if (entity.MapId != fromMapId || entity.Position != fromPosition)
        {
            Redraw(entity);
            _eventBus.Publish(new MobileMovedEvent(entity.Id, fromMapId, fromPosition, entity.MapId, entity.Position));
        }

        return true;
    }
}
