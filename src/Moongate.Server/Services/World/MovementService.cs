using Moongate.Core.Extensions;
using Moongate.Core.Geometry;
using Moongate.Core.Interfaces;
using Moongate.Core.Primitives;
using Moongate.Core.Types;
using Moongate.Network.Packets.Outgoing;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Data.Events;
using Moongate.Server.Abstractions.Data.Session;
using Moongate.Server.Abstractions.Interfaces.World;
using Moongate.Server.Data.Internal.World;
using Moongate.UO.Data.Mobiles;
using Moongate.Server.Abstractions.Data.Internal;
using Moongate.Server.Abstractions.Types;
using Moongate.Server.Abstractions.Data.Config;
using Moongate.UO.Data.Types;
using SquidStd.Core.Interfaces.Events;
using SquidStd.Persistence.Abstractions.Interfaces.Persistence;

namespace Moongate.Server.Services.World;

/// <summary>
/// Default <see cref="IMovementService" />. <see cref="Evaluate" /> is the pure decision core (rate
/// limit, turn-vs-step, region, tile) — public and static so it is unit-testable without a live
/// <see cref="PlayerSession" />, mirroring <c>WorldService.IsRecipient</c>. <see cref="TryMove" /> is
/// the impure orchestrator: reads/writes the session, persists, re-indexes, broadcasts, replies.
/// </summary>
public sealed class MovementService : IMovementService
{
    // ModernUO's MovementImpl.WalkFootDelay / RunFootDelay, verified against others/ModernUO.
    private static readonly TimeSpan WalkInterval = TimeSpan.FromMilliseconds(400);
    private static readonly TimeSpan RunInterval = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// What a step costs before the next one comes due. Turning is free — ModernUO's TurnDelay is 0 —
    /// so only an actual step moves the clock.
    /// </summary>
    public static TimeSpan CostOf(MobileEntity mobile, DirectionType direction)
        => direction.StripRunning() != mobile.Direction.StripRunning()
               ? TimeSpan.Zero
               : direction.IsRunning() ? RunInterval : WalkInterval;

    /// <summary>
    /// Whether a step arriving at <paramref name="now" /> is due, and the slack left afterwards.
    /// <para>
    /// A hard threshold refuses anything even a millisecond early, and network jitter delivers that
    /// constantly — each refusal snaps the client back to the server's position, which is what makes
    /// walking stutter. So arriving late rebuilds slack up to <paramref name="maxCredit" />, arriving
    /// early spends it, and only once the slack is in debt past that same limit does the step wait its
    /// turn. This is ModernUO's credit buffer; its extra allowance for high-latency players is
    /// deliberately not copied, since nothing here measures round-trip time.
    /// </para>
    /// </summary>
    public static MovementThrottleDecision Throttle(
        DateTimeOffset now,
        DateTimeOffset nextMoveAt,
        TimeSpan credit,
        TimeSpan maxCredit
    )
    {
        var delta = now - nextMoveAt;

        if (delta >= TimeSpan.Zero)
        {
            var rebuilt = credit + delta;

            return new(MovementThrottleVerdictType.Run, rebuilt > maxCredit ? maxCredit : rebuilt);
        }

        var spent = credit + delta;

        return spent >= -maxCredit
                   ? new(MovementThrottleVerdictType.Run, spent)
                   : new(MovementThrottleVerdictType.Queue, credit);
    }

    private readonly IMapTileService _mapTiles;
    private readonly IRegionService _regions;
    private readonly ISpatialIndexService _spatial;
    private readonly IWorldService _world;
    private readonly IEntityStore<MobileEntity, Serial> _mobiles;
    private readonly TimeProvider _timeProvider;
    private readonly IEventBus _eventBus;
    private readonly ILoopAffinity? _loopAffinity;
    private readonly TimeSpan _maxCredit;
    private readonly int _queueLimit;

    public MovementService(
        IMapTileService mapTiles,
        IRegionService regions,
        ISpatialIndexService spatial,
        IWorldService world,
        IPersistenceService persistenceService,
        TimeProvider timeProvider,
        IEventBus eventBus,
        MoongateConfig? config = null,
        ILoopAffinity? loopAffinity = null
    )
    {
        var network = config?.Network;
        _maxCredit = TimeSpan.FromMilliseconds(Math.Max(network?.MovementCreditMilliseconds ?? 200, 0));
        _queueLimit = Math.Max(network?.MovementQueueLimit ?? 10, 1);
        _mapTiles = mapTiles;
        _regions = regions;
        _spatial = spatial;
        _world = world;
        _mobiles = persistenceService.GetStore<MobileEntity, Serial>();
        _timeProvider = timeProvider;
        _eventBus = eventBus;
        _loopAffinity = loopAffinity;
    }

    /// <summary>
    /// Pure decision core: no I/O, no session, no persistence. Returns what the caller should do —
    /// accept-and-apply, or reject — without doing it.
    /// </summary>
    public static MovementDecision Evaluate(
        MobileEntity mobile,
        DirectionType direction,
        byte sequence,
        byte? lastSequence,
        DateTimeOffset lastMoveAt,
        DateTimeOffset now,
        IMapTileService mapTiles,
        IRegionService regionService,
        IReadOnlyList<ItemEntity> groundItems
    )
    {
        var expected = lastSequence is { } last ? WrapNextSequence(last) : (byte)0;

        if (sequence != expected)
        {
            return RejectDecision(mobile);
        }

        var isTurnOnly = direction.StripRunning() != mobile.Direction.StripRunning();

        if (isTurnOnly)
        {
            return new(true, false, mobile.Position, direction);
        }

        var (dx, dy) = direction.ToOffset();
        var target = new Point3D(mobile.Position.X + dx, mobile.Position.Y + dy, mobile.Position.Z);

        if (regionService.At((MapType)mobile.MapId, target)?.IsImpassable == true)
        {
            return RejectDecision(mobile);
        }

        if (!mapTiles.TryGetWalkableZ(mobile.MapId, target.X, target.Y, mobile.Position.Z, groundItems, out var newZ))
        {
            return RejectDecision(mobile);
        }

        return new(true, true, new(target.X, target.Y, newZ), direction);
    }

    public void TryMove(PlayerSession session, DirectionType direction, byte sequence)
    {
        _loopAffinity?.AssertOnLoop("movement.try_move");

        var mobile = session.Character;

        if (mobile is null)
        {
            return;
        }

        var now = _timeProvider.GetUtcNow();

        // Chebyshev range 1 around the mobile's current position covers every tile a single step
        // could land on (target is always exactly distance 1 away); MapTileService filters these
        // down to the exact target tile itself.
        var groundItems = _spatial.GetItemsInRange(mobile.MapId, mobile.Position, 1);

        // Steps already waiting must keep their order, so a new one joins the back of the queue
        // rather than overtaking them.
        if (session.MovementQueue.Count > 0)
        {
            Enqueue(session, mobile, direction, sequence);

            return;
        }

        var gate = Throttle(now, session.NextMoveAt, session.MovementCredit, _maxCredit);

        if (gate.Verdict == MovementThrottleVerdictType.Queue)
        {
            Enqueue(session, mobile, direction, sequence);

            return;
        }

        session.SetMovementCredit(gate.Credit);
        Step(session, mobile, direction, sequence, now, groundItems);
    }

    /// <summary>
    /// Runs the steps that have come due, oldest first, and stops at the first one that has not. The
    /// game loop calls this; without it the tail of a queue would sit there until the player moved
    /// again, which reads as the character stopping a step short.
    /// </summary>
    public void DrainQueue(PlayerSession session)
    {
        _loopAffinity?.AssertOnLoop("movement.drain_queue");

        var mobile = session.Character;

        if (mobile is null)
        {
            session.ClearMovementQueue();

            return;
        }

        var now = _timeProvider.GetUtcNow();

        while (session.MovementQueue.Count > 0 && now >= session.NextMoveAt)
        {
            var queued = session.MovementQueue.Dequeue();
            var groundItems = _spatial.GetItemsInRange(mobile.MapId, mobile.Position, 1);

            if (!Step(session, mobile, queued.Direction, queued.Sequence, now, groundItems))
            {
                return;
            }
        }
    }

    /// <summary>
    /// Takes one step if it is legal, and reports whether it was. A refusal resyncs the client and
    /// drops everything still queued: those steps describe a walk that no longer happened.
    /// </summary>
    private bool Step(
        PlayerSession session,
        MobileEntity mobile,
        DirectionType direction,
        byte sequence,
        DateTimeOffset now,
        IReadOnlyList<ItemEntity> groundItems
    )
    {
        var decision = Evaluate(
            mobile,
            direction,
            sequence,
            session.LastMoveSequence,
            session.LastMoveAt,
            now,
            _mapTiles,
            _regions,
            groundItems
        );

        if (!decision.Accepted)
        {
            // Any rejection forces a resync: the next packet's sequence is accepted unconditionally,
            // matching how a real UO client resets its counter after a rejected move.
            session.SetLastMove(null, session.LastMoveAt);
            session.ClearMovementQueue();
            Reject(session, mobile, sequence);

            return false;
        }

        session.SetLastMove(sequence, now);
        session.SetNextMoveAt(now + CostOf(mobile, direction));
        Apply(mobile, decision);
        Accept(session, mobile, sequence);

        return true;
    }

    /// <summary>
    /// Holds a step that arrived too early. Past the limit the client is not merely fast — the UO
    /// client leaves at most five movements unacknowledged — so it is resynced instead.
    /// </summary>
    private void Enqueue(PlayerSession session, MobileEntity mobile, DirectionType direction, byte sequence)
    {
        if (session.MovementQueue.Count >= _queueLimit)
        {
            session.SetLastMove(null, session.LastMoveAt);
            session.ClearMovementQueue();
            Reject(session, mobile, sequence);

            return;
        }

        session.MovementQueue.Enqueue(new(direction, sequence));
    }

    public bool TryMoveNpc(Serial mobileId, DirectionType direction)
    {
        _loopAffinity?.AssertOnLoop("movement.try_move_npc");

        var mobile = _mobiles.GetById(mobileId);

        if (mobile is null || string.IsNullOrWhiteSpace(mobile.BrainScriptId))
        {
            return false;
        }

        var now = _timeProvider.GetUtcNow();
        var groundItems = _spatial.GetItemsInRange(mobile.MapId, mobile.Position, 1);
        var decision = Evaluate(
            mobile,
            direction,
            0,
            null,
            DateTimeOffset.MinValue,
            now,
            _mapTiles,
            _regions,
            groundItems
        );

        if (!decision.Accepted)
        {
            return false;
        }

        Apply(mobile, decision);

        return true;
    }

    private void Accept(PlayerSession session, MobileEntity mobile, byte sequence)
        => session.Send(new MovementAckPacket(sequence, Notoriety.Resolve(mobile.Kills, mobile.Criminal)));

    private void Apply(MobileEntity mobile, MovementDecision decision)
    {
        var fromMapId = mobile.MapId;
        var fromPosition = mobile.Position;

        mobile.Direction = decision.NewDirection;

        if (decision.PositionChanged)
        {
            mobile.Position = decision.NewPosition;
        }

        _mobiles.UpsertAsync(mobile).WaitSync();
        _spatial.AddOrUpdate(mobile);

        if (decision.PositionChanged)
        {
            _eventBus.Publish(new MobileMovedEvent(mobile.Id, fromMapId, fromPosition, mobile.MapId, mobile.Position));
        }
    }

    // The 0x77 that used to live here is now the third row of IVisibilityService.UpdateFor, sent
    // only to clients that know the serial -- this broadcast reached every session in range,
    // including ones that had never been told the mobile exists.

    private void Reject(PlayerSession session, MobileEntity mobile, byte sequence)
        => session.Send(
            new MoveRejectPacket(
                sequence,
                (ushort)mobile.Position.X,
                (ushort)mobile.Position.Y,
                mobile.Direction,
                (sbyte)mobile.Position.Z
            )
        );

    // Pure rejection shorthand for Evaluate's four "current position/direction, unchanged" exits.
    // Not to be confused with the instance Reject(PlayerSession, MobileEntity, byte) below, which
    // sends the MoveRejectPacket to the client.
    private static MovementDecision RejectDecision(MobileEntity mobile)
        => new(false, false, mobile.Position, mobile.Direction);

    // ModernUO wraps the move-sequence counter 255 -> 1, never to 0: sequence 0 is a reserved
    // sentinel meaning "no baseline / just reset" (MovementThrottle.cs:272-277, verified against
    // others/ModernUO).
    private static byte WrapNextSequence(byte sequence)
        => sequence == 255 ? (byte)1 : (byte)(sequence + 1);
}
