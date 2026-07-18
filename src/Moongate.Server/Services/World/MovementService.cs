using Moongate.Core.Extensions;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Core.Types;
using Moongate.Network.Packets.Outgoing;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Data.Session;
using Moongate.Server.Abstractions.Interfaces.World;
using Moongate.Server.Data.Internal.World;
using Moongate.UO.Data.Mobiles;
using Moongate.UO.Data.Types;
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

    // Classic UO client view range.
    private const int ViewRange = 18;

    private readonly IMapTileService _mapTiles;
    private readonly IRegionService _regions;
    private readonly ISpatialIndexService _spatial;
    private readonly IWorldService _world;
    private readonly IEntityStore<MobileEntity, Serial> _mobiles;
    private readonly TimeProvider _timeProvider;

    public MovementService(
        IMapTileService mapTiles,
        IRegionService regions,
        ISpatialIndexService spatial,
        IWorldService world,
        IPersistenceService persistenceService,
        TimeProvider timeProvider
    )
    {
        _mapTiles = mapTiles;
        _regions = regions;
        _spatial = spatial;
        _world = world;
        _mobiles = persistenceService.GetStore<MobileEntity, Serial>();
        _timeProvider = timeProvider;
    }

    public void TryMove(PlayerSession session, DirectionType direction, byte sequence)
    {
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
            // matching how a real UO client resets its counter after a rejected move. The timing
            // baseline (LastMoveAt) is left untouched so a burst of illegal attempts cannot keep
            // resetting the clock and starve the rate limiter.
            session.SetLastMove(null, session.LastMoveAt);
            Reject(session, mobile, sequence);

            return;
        }

        session.SetLastMove(sequence, now);
        mobile.Direction = decision.NewDirection;

        if (decision.PositionChanged)
        {
            mobile.Position = decision.NewPosition;
        }

        _mobiles.UpsertAsync(mobile).WaitSync();
        _spatial.AddOrUpdate(mobile);
        Broadcast(mobile);
        Accept(session, mobile, sequence);
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
        var expected = lastSequence is { } last ? unchecked((byte)(last + 1)) : sequence;

        if (sequence != expected)
        {
            return new(false, false, mobile.Position, mobile.Direction);
        }

        var isTurnOnly = direction.StripRunning() != mobile.Direction.StripRunning();

        // Turning has no timing gate (ModernUO: TurnDelay = 0); only an actual step does.
        if (!isTurnOnly)
        {
            var minInterval = direction.IsRunning() ? RunInterval : WalkInterval;

            if (now - lastMoveAt < minInterval)
            {
                return new(false, false, mobile.Position, mobile.Direction);
            }
        }

        if (isTurnOnly)
        {
            return new(true, false, mobile.Position, direction);
        }

        var (dx, dy) = direction.ToOffset();
        var target = new Point3D(mobile.Position.X + dx, mobile.Position.Y + dy, mobile.Position.Z);

        if (regionService.At((MapType)mobile.MapId, target)?.IsImpassable == true)
        {
            return new(false, false, mobile.Position, mobile.Direction);
        }

        if (!mapTiles.TryGetWalkableZ(mobile.MapId, target.X, target.Y, mobile.Position.Z, groundItems, out var newZ))
        {
            return new(false, false, mobile.Position, mobile.Direction);
        }

        return new(true, true, new(target.X, target.Y, newZ), direction);
    }

    private void Accept(PlayerSession session, MobileEntity mobile, byte sequence)
        => session.Send(new MovementAckPacket(sequence, Notoriety.Resolve(mobile.Kills, mobile.Criminal)));

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

    private void Broadcast(MobileEntity mobile)
        => _world.SendToPlayersInRange(
            mobile.MapId,
            mobile.Position,
            ViewRange,
            new UpdatePlayerPacket(
                mobile.Id,
                (ushort)mobile.Body,
                (ushort)mobile.Position.X,
                (ushort)mobile.Position.Y,
                (sbyte)mobile.Position.Z,
                mobile.Direction,
                mobile.SkinHue,
                0,
                Notoriety.Resolve(mobile.Kills, mobile.Criminal)
            ),
            exclude: mobile.Id
        );
}
