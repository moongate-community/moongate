using Moongate.Core.Interfaces;
using Moongate.Core.Primitives;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Data.Config;
using Moongate.Server.Abstractions.Data.Events;
using Moongate.Server.Abstractions.Data.World;
using Moongate.Server.Abstractions.Interfaces.AI;
using Moongate.Server.Abstractions.Interfaces.World;
using SquidStd.Abstractions.Interfaces.Services;
using SquidStd.Core.Interfaces.Events;

namespace Moongate.Server.Services.AI;

/// <summary>
/// Maintains the 3×3 sector coverage around online players. It is game-loop owned: event subscribers and
/// its single repeating timer both execute on that loop, so its dictionaries need no synchronization.
/// </summary>
public sealed class SectorActivityService : ISectorActivityService, ISquidStdService
{
    private const string TimerName = "npc-sector-activity";
    private const int SectorShift = 4;

    private readonly IGameLoopContext _loop;
    private readonly IEventBus _eventBus;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _idleGrace;
    private readonly TimeSpan _tickInterval;
    private readonly INpcAiMetrics _metrics;
    private readonly Dictionary<Serial, (int MapId, int SectorX, int SectorY)> _players = [];
    private readonly Dictionary<(int MapId, int SectorX, int SectorY), SectorState> _sectors = [];

    private string? _timerId;

    public SectorActivityService(
        IGameLoopContext loop,
        IEventBus eventBus,
        TimeProvider timeProvider,
        MoongateConfig config,
        INpcAiMetrics metrics
    )
    {
        _loop = loop;
        _eventBus = eventBus;
        _timeProvider = timeProvider;
        _idleGrace = TimeSpan.FromSeconds(config.NpcAi.Advanced.SectorIdleGraceSeconds);
        _tickInterval = TimeSpan.FromMilliseconds(config.NpcAi.Advanced.MinTickMilliseconds);
        _metrics = metrics;
    }

    public SectorActivitySnapshot Current => new(
        _sectors.Count(pair => pair.Value.ReferenceCount > 0),
        _sectors.Count(pair => pair.Value.ReferenceCount == 0)
    );

    public bool IsActive(int mapId, int sectorX, int sectorY)
        => _sectors.ContainsKey((mapId, sectorX, sectorY));

    public void TrackPlayer(MobileEntity player)
    {
        var location = (
            MapId: player.MapId,
            SectorX: player.Position.X >> SectorShift,
            SectorY: player.Position.Y >> SectorShift
        );

        if (_players.TryGetValue(player.Id, out var previous))
        {
            if (previous == location)
            {
                return;
            }

            MovePlayer(player.Id, location.MapId, location.Item2, location.Item3);

            return;
        }

        _players[player.Id] = location;

        foreach (var sector in Coverage(location))
        {
            Acquire(sector);
        }
    }

    public void MovePlayer(Serial playerId, int mapId, int sectorX, int sectorY)
    {
        if (!_players.TryGetValue(playerId, out var previous))
        {
            return;
        }

        var next = (MapId: mapId, SectorX: sectorX, SectorY: sectorY);

        if (previous == next)
        {
            return;
        }

        var previousCoverage = Coverage(previous).ToHashSet();
        var nextCoverage = Coverage(next).ToHashSet();

        foreach (var sector in previousCoverage.Except(nextCoverage))
        {
            Release(sector);
        }

        foreach (var sector in nextCoverage.Except(previousCoverage))
        {
            Acquire(sector);
        }

        _players[playerId] = next;
    }

    public void UntrackPlayer(Serial playerId)
    {
        if (!_players.Remove(playerId, out var location))
        {
            return;
        }

        foreach (var sector in Coverage(location))
        {
            Release(sector);
        }
    }

    public void Tick()
    {
        var now = _timeProvider.GetUtcNow();
        var expired = _sectors
            .Where(pair => pair.Value.ReferenceCount == 0 && pair.Value.DeactivateAt is { } deactivateAt && deactivateAt <= now)
            .Select(pair => pair.Key)
            .ToArray();

        foreach (var sector in expired)
        {
            _sectors.Remove(sector);
            _eventBus.Publish(new SectorDeactivatedEvent(sector.MapId, sector.SectorX, sector.SectorY));
        }

        UpdateMetrics();
    }

    public ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        if (_timerId is null)
        {
            _timerId = _loop.ScheduleRepeating(TimerName, _tickInterval, Tick);
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        if (_timerId is not null)
        {
            _loop.Cancel(_timerId);
            _timerId = null;
        }

        return ValueTask.CompletedTask;
    }

    private void Acquire((int MapId, int SectorX, int SectorY) sector)
    {
        if (!_sectors.TryGetValue(sector, out var state))
        {
            state = new();
            _sectors[sector] = state;
        }

        var wasInactive = state.ReferenceCount == 0;
        state.ReferenceCount++;

        if (wasInactive)
        {
            state.DeactivateAt = null;
            _eventBus.Publish(new SectorActivatedEvent(sector.MapId, sector.SectorX, sector.SectorY));
        }

        UpdateMetrics();
    }

    private void Release((int MapId, int SectorX, int SectorY) sector)
    {
        var state = _sectors[sector];
        state.ReferenceCount--;

        if (state.ReferenceCount == 0)
        {
            state.DeactivateAt = _timeProvider.GetUtcNow() + _idleGrace;
        }

        UpdateMetrics();
    }

    private IEnumerable<(int MapId, int SectorX, int SectorY)> Coverage((int MapId, int SectorX, int SectorY) center)
    {
        for (var sectorX = center.SectorX - 1; sectorX <= center.SectorX + 1; sectorX++)
        {
            for (var sectorY = center.SectorY - 1; sectorY <= center.SectorY + 1; sectorY++)
            {
                yield return (center.MapId, sectorX, sectorY);
            }
        }
    }

    private void UpdateMetrics()
    {
        _metrics.SetActiveSectorCounts(
            _sectors.Count(pair => pair.Value.ReferenceCount > 0),
            _sectors.Count(pair => pair.Value.ReferenceCount == 0)
        );
    }

    private sealed class SectorState
    {
        public int ReferenceCount { get; set; }

        public DateTimeOffset? DeactivateAt { get; set; }
    }
}
