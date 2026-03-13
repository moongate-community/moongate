using Moongate.Core.Extensions.Strings;
using Moongate.Server.Data.World;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Timing;
using Moongate.Server.Interfaces.Services.World;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Data.Events.Spatial;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Interfaces.Templates;
using Moongate.UO.Data.Maps;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Utils;
using Serilog;

namespace Moongate.Server.Services.World;

/// <summary>
/// Runtime spawner service that activates spawners from currently active sectors.
/// </summary>
public sealed class SpawnService : ISpawnService
{
    private const string TimerName = "spawn_runtime_tick";
    private const string SpawnOriginKey = "spawn_origin";
    private const string SpawnWalkingRangeKey = "walking_range";
    private const string SpawnHomeRangeKey = "home_range";
    private const string SpawnerIdKey = "spawner_id";
    private const int ScanIntervalMilliseconds = 3000;
    private const int TickIntervalMilliseconds = 1000;
    private const int ActivationRange = MapSectorConsts.SectorSize * 2;

    private readonly ILogger _logger = Log.ForContext<SpawnService>();
    private readonly ITimerService _timerService;
    private readonly ISpatialWorldService _spatialWorldService;
    private readonly IMobileService _mobileService;
    private readonly IMobileTemplateService _mobileTemplateService;
    private readonly IGameEventBusService _gameEventBusService;
    private readonly ISpawnsDataService _spawnsDataService;
    private readonly Lock _sync = new();
    private readonly Dictionary<Serial, RuntimeSpawnerState> _states = [];
    private readonly Dictionary<Guid, SpawnDefinitionEntry> _definitionsByGuid = [];
    private int _isTickRunning;
    private string? _timerId;
    private long _lastScanAt;

    public SpawnService(
        ITimerService timerService,
        ISpatialWorldService spatialWorldService,
        ISpawnsDataService spawnsDataService,
        IMobileService mobileService,
        IMobileTemplateService mobileTemplateService,
        IGameEventBusService gameEventBusService
    )
    {
        _timerService = timerService;
        _spatialWorldService = spatialWorldService;
        _spawnsDataService = spawnsDataService;
        _mobileService = mobileService;
        _mobileTemplateService = mobileTemplateService;
        _gameEventBusService = gameEventBusService;
    }

    /// <inheritdoc />
    public int GetTrackedSpawnerCount()
    {
        lock (_sync)
        {
            return _states.Count;
        }
    }

    /// <inheritdoc />
    public async Task<bool> TriggerAsync(Serial spawnerItemId, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;

        if (spawnerItemId == Serial.Zero)
        {
            return false;
        }

        if (!TryResolveRuntimeState(spawnerItemId, out var state))
        {
            return false;
        }

        var now = Environment.TickCount64;

        return await TrySpawnForStateAsync(state, now, forceSpawn: true);
    }

    /// <inheritdoc />
    public async Task<bool> TriggerAsync(UOItemEntity spawnerItem, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;

        ArgumentNullException.ThrowIfNull(spawnerItem);

        if (spawnerItem.Id == Serial.Zero)
        {
            return false;
        }

        if (!TryBuildStateFromItem(spawnerItem, out var state))
        {
            return false;
        }

        lock (_sync)
        {
            _states[spawnerItem.Id] = state;
        }

        var now = Environment.TickCount64;

        return await TrySpawnForStateAsync(state, now, forceSpawn: true);
    }

    /// <inheritdoc />
    public Task StartAsync()
    {
        BuildDefinitionIndex();
        _timerId = _timerService.RegisterTimer(
            TimerName,
            TimeSpan.FromMilliseconds(TickIntervalMilliseconds),
            OnTick,
            repeat: true
        );
        _logger.Information("SpawnService started. LoadedDefinitions={Count}", _definitionsByGuid.Count);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync()
    {
        if (!string.IsNullOrWhiteSpace(_timerId))
        {
            _timerService.UnregisterTimer(_timerId);
            _timerId = null;
        }

        lock (_sync)
        {
            _states.Clear();
        }

        _logger.Information("SpawnService stopped.");

        return Task.CompletedTask;
    }

    private void BuildDefinitionIndex()
    {
        _definitionsByGuid.Clear();

        foreach (var mapId in Map.MapIDs)
        {
            foreach (var definition in _spawnsDataService.GetEntriesByMap(mapId))
            {
                _definitionsByGuid[definition.Guid] = definition;
            }
        }
    }

    private void OnTick()
    {
        if (Interlocked.Exchange(ref _isTickRunning, 1) != 0)
        {
            return;
        }

        _ = ProcessTickAsync()
            .ContinueWith(
                _ => Interlocked.Exchange(ref _isTickRunning, 0),
                TaskScheduler.Default
            );
    }

    private async Task ProcessTickAsync()
    {
        try
        {
            var now = Environment.TickCount64;

            if (now - _lastScanAt >= ScanIntervalMilliseconds)
            {
                ScanActiveSectorsForSpawners();
                _lastScanAt = now;
            }

            List<RuntimeSpawnerState> dueStates;

            lock (_sync)
            {
                dueStates =
                [
                    .. _states.Values
                              .Where(state => state.NextSpawnAt <= now)
                              .Select(state => state with { })
                ];
            }

            foreach (var state in dueStates)
            {
                _ = await TrySpawnForStateAsync(state, now, forceSpawn: false);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "SpawnService tick failed.");
        }
    }

    private void ScanActiveSectorsForSpawners()
    {
        var activeSectors = _spatialWorldService.GetActiveSectors();

        if (activeSectors.Count == 0)
        {
            return;
        }

        foreach (var sector in activeSectors)
        {
            foreach (var item in sector.GetItems())
            {
                if (item.ParentContainerId != Serial.Zero || item.EquippedMobileId != Serial.Zero)
                {
                    continue;
                }

                if (!item.TryGetCustomString(SpawnerIdKey, out var spawnerIdRaw) ||
                    string.IsNullOrWhiteSpace(spawnerIdRaw))
                {
                    continue;
                }

                if (!Guid.TryParse(spawnerIdRaw, out var spawnerGuid))
                {
                    _logger.Debug(
                        "Skipping spawner item {ItemId}: invalid spawner_id '{SpawnerIdRaw}'.",
                        item.Id,
                        spawnerIdRaw
                    );

                    continue;
                }

                if (spawnerGuid == Guid.Empty)
                {
                    _logger.Debug(
                        "Skipping spawner item {ItemId}: spawner_id is empty GUID.",
                        item.Id
                    );

                    continue;
                }

                if (!_definitionsByGuid.TryGetValue(spawnerGuid, out var definition))
                {
                    _logger.Debug(
                        "Skipping spawner item {ItemId}: spawner_id {SpawnerGuid} not found in loaded definitions.",
                        item.Id,
                        spawnerGuid
                    );

                    continue;
                }

                lock (_sync)
                {
                    if (_states.TryGetValue(item.Id, out var existing))
                    {
                        _states[item.Id] = existing with
                        {
                            MapId = item.MapId,
                            Location = item.Location,
                            Definition = definition
                        };
                    }
                    else
                    {
                        _states[item.Id] = new(
                            item.Id,
                            spawnerGuid,
                            item.MapId,
                            item.Location,
                            definition,
                            Environment.TickCount64 + ComputeNextDelayMilliseconds(definition)
                        );
                    }
                }
            }
        }
    }

    private async Task<bool> TrySpawnForStateAsync(RuntimeSpawnerState state, long now, bool forceSpawn)
    {
        if (!forceSpawn && _spatialWorldService.GetPlayersInRange(state.Location, ActivationRange, state.MapId).Count == 0)
        {
            Reschedule(state.SpawnerItemId, state.Definition, now);

            return false;
        }

        var currentCount = CountSpawnedMobiles(state);

        if (!forceSpawn && currentCount >= Math.Max(1, state.Definition.Count))
        {
            Reschedule(state.SpawnerItemId, state.Definition, now);

            return false;
        }

        var resolvedSpawnEntry = ResolveSpawnEntry(state.Definition.Entries);

        if (resolvedSpawnEntry is null)
        {
            Reschedule(state.SpawnerItemId, state.Definition, now);

            return false;
        }

        var spawnedMobile = false;

        try
        {
            var (spawned, mobile) = await _mobileService.TrySpawnFromTemplateAsync(
                                        resolvedSpawnEntry.TemplateId,
                                        state.Location,
                                        state.MapId,
                                        accountId: null
                                    );

            if (!spawned)
            {
                mobile = await _mobileService.SpawnFromTemplateAsync(
                             "generic_npc",
                             state.Location,
                             state.MapId,
                             accountId: null
                         );

                _logger.Warning(
                    "Failed to resolve template {TemplateId} for spawner {SpawnerGuid}. Spawned fallback mobile {MobileId} instead.",
                    resolvedSpawnEntry.TemplateId,
                    state.SpawnGuid,
                    mobile.Id
                );
            }

            mobile.SetCustomString(SpawnOriginKey, state.SpawnGuid.ToString("D"));
            mobile.SetCustomInteger(SpawnWalkingRangeKey, state.Definition.WalkingRange);
            mobile.SetCustomInteger(SpawnHomeRangeKey, state.Definition.HomeRange);
            await _mobileService.CreateOrUpdateAsync(mobile);
            _spatialWorldService.AddOrUpdateMobile(mobile);
            await _gameEventBusService.PublishAsync(
                new MobileSpawnedFromSpawnerEvent(
                    mobile,
                    state.SpawnGuid,
                    state.Definition.Name,
                    state.Definition.SourceGroup,
                    state.Definition.SourceFile,
                    state.Definition.Location,
                    state.Definition.Count,
                    state.Definition.MinDelay,
                    state.Definition.MaxDelay,
                    state.Definition.Team,
                    state.Definition.HomeRange,
                    state.Definition.WalkingRange,
                    resolvedSpawnEntry.Entry.Name,
                    resolvedSpawnEntry.Entry.MaxCount,
                    resolvedSpawnEntry.Entry.Probability
                )
            );
            spawnedMobile = true;

            _logger.Verbose(
                "Spawned mobile from spawner {SpawnerGuid}: MobileId={MobileId} Template={TemplateId}",
                state.SpawnGuid,
                mobile.Id,
                resolvedSpawnEntry.TemplateId
            );
        }
        catch (Exception ex)
        {
            _logger.Warning(
                ex,
                "Failed to spawn mobile from spawner {SpawnerGuid} using template {TemplateId}.",
                state.SpawnGuid,
                resolvedSpawnEntry.TemplateId
            );
        }
        finally
        {
            Reschedule(state.SpawnerItemId, state.Definition, now);
        }

        return spawnedMobile;
    }

    private int CountSpawnedMobiles(RuntimeSpawnerState state)
    {
        var range = Math.Max(1, state.Definition.HomeRange);
        var spawnOrigin = state.SpawnGuid.ToString("D");
        var count = 0;

        foreach (var mobile in _spatialWorldService.GetNearbyMobiles(state.Location, range, state.MapId))
        {
            if (mobile.IsPlayer)
            {
                continue;
            }

            if (!mobile.TryGetCustomString(SpawnOriginKey, out var origin))
            {
                continue;
            }

            if (string.Equals(origin, spawnOrigin, StringComparison.OrdinalIgnoreCase))
            {
                count++;
            }
        }

        return count;
    }

    private void Reschedule(Serial spawnerItemId, SpawnDefinitionEntry definition, long now)
    {
        lock (_sync)
        {
            if (!_states.TryGetValue(spawnerItemId, out var state))
            {
                return;
            }

            _states[spawnerItemId] = state with
            {
                NextSpawnAt = now + ComputeNextDelayMilliseconds(definition)
            };
        }
    }

    private bool TryResolveRuntimeState(Serial spawnerItemId, out RuntimeSpawnerState state)
    {
        lock (_sync)
        {
            if (_states.TryGetValue(spawnerItemId, out state))
            {
                return true;
            }
        }

        foreach (var sector in _spatialWorldService.GetActiveSectors())
        {
            foreach (var item in sector.GetItems())
            {
                if (item.Id != spawnerItemId)
                {
                    continue;
                }

                if (!TryBuildStateFromItem(item, out state))
                {
                    return false;
                }

                lock (_sync)
                {
                    _states[spawnerItemId] = state;
                }

                return true;
            }
        }

        state = default;

        return false;
    }

    private bool TryBuildStateFromItem(UOItemEntity item, out RuntimeSpawnerState state)
    {
        state = default;

        if (!item.TryGetCustomString(SpawnerIdKey, out var spawnerIdRaw) ||
            string.IsNullOrWhiteSpace(spawnerIdRaw))
        {
            _logger.Debug("Cannot trigger spawner {ItemId}: missing spawner_id custom property.", item.Id);

            return false;
        }

        if (!Guid.TryParse(spawnerIdRaw, out var spawnerGuid) || spawnerGuid == Guid.Empty)
        {
            _logger.Debug(
                "Cannot trigger spawner {ItemId}: invalid spawner_id '{SpawnerIdRaw}'.",
                item.Id,
                spawnerIdRaw
            );

            return false;
        }

        if (!_definitionsByGuid.TryGetValue(spawnerGuid, out var definition))
        {
            _logger.Debug(
                "Cannot trigger spawner {ItemId}: spawner_id {SpawnerGuid} not found in definitions.",
                item.Id,
                spawnerGuid
            );

            return false;
        }

        state = new(
            item.Id,
            spawnerGuid,
            item.MapId,
            item.Location,
            definition,
            Environment.TickCount64 + ComputeNextDelayMilliseconds(definition)
        );

        return true;
    }

    private ResolvedSpawnEntry? ResolveSpawnEntry(IReadOnlyList<SpawnEntryDefinition> entries)
    {
        if (entries.Count == 0)
        {
            return null;
        }

        var weighted = entries.Where(static entry => entry.Probability > 0).ToList();

        if (weighted.Count == 0)
        {
            return null;
        }

        var totalWeight = weighted.Sum(static entry => entry.Probability);
        var roll = Random.Shared.Next(1, totalWeight + 1);
        var cumulative = 0;

        foreach (var entry in weighted)
        {
            cumulative += entry.Probability;

            if (roll > cumulative)
            {
                continue;
            }

            var resolved = ResolveTemplateId(entry.Name);

            // TODO: remove only for debug

            if (string.IsNullOrEmpty(resolved))
            {
                resolved = "generic_npc";
            }

            if (resolved is not null)
            {
                return new ResolvedSpawnEntry(resolved, entry);
            }
        }

        foreach (var entry in weighted)
        {
            var resolved = ResolveTemplateId(entry.Name);

            if (resolved is not null)
            {
                return new ResolvedSpawnEntry(resolved, entry);
            }
        }

        return null;
    }

    private string? ResolveTemplateId(string sourceName)
    {
        if (string.IsNullOrWhiteSpace(sourceName))
        {
            return null;
        }

        var candidate = sourceName.Trim();

        if (_mobileTemplateService.TryGet(candidate, out _))
        {
            return candidate;
        }

        var snakeCase = candidate.ToSnakeCase();

        if (_mobileTemplateService.TryGet(snakeCase, out _))
        {
            return snakeCase;
        }

        return null;
    }

    private static long ComputeNextDelayMilliseconds(SpawnDefinitionEntry definition)
    {
        var minDelay = definition.MinDelay <= TimeSpan.Zero ? TimeSpan.FromSeconds(10) : definition.MinDelay;
        var maxDelay = definition.MaxDelay <= TimeSpan.Zero ? minDelay : definition.MaxDelay;

        if (maxDelay < minDelay)
        {
            (minDelay, maxDelay) = (maxDelay, minDelay);
        }

        if (maxDelay == minDelay)
        {
            return (long)minDelay.TotalMilliseconds;
        }

        var minMs = Math.Max(1L, (long)minDelay.TotalMilliseconds);
        var maxMs = Math.Max(minMs, (long)maxDelay.TotalMilliseconds);

        return Random.Shared.NextInt64(minMs, maxMs + 1);
    }

    private sealed record RuntimeSpawnerState(
        Serial SpawnerItemId,
        Guid SpawnGuid,
        int MapId,
        Moongate.UO.Data.Geometry.Point3D Location,
        SpawnDefinitionEntry Definition,
        long NextSpawnAt
    );

    private sealed record ResolvedSpawnEntry(string TemplateId, SpawnEntryDefinition Entry);
}
