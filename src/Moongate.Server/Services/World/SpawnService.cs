using Moongate.Core.Extensions.Strings;
using Moongate.Server.Data.World;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Interfaces.Services.Timing;
using Moongate.Server.Interfaces.Services.World;
using Moongate.Server.Interfaces.Services.Spatial;
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
    private const string SpawnerIdKey = "spawner_id";
    private const int ScanIntervalMilliseconds = 3000;
    private const int TickIntervalMilliseconds = 1000;
    private const int ActivationRange = MapSectorConsts.SectorSize * 2;

    private readonly ILogger _logger = Log.ForContext<SpawnService>();
    private readonly ITimerService _timerService;
    private readonly ISpatialWorldService _spatialWorldService;
    private readonly IMobileService _mobileService;
    private readonly IMobileTemplateService _mobileTemplateService;
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
        IMobileTemplateService mobileTemplateService
    )
    {
        _timerService = timerService;
        _spatialWorldService = spatialWorldService;
        _spawnsDataService = spawnsDataService;
        _mobileService = mobileService;
        _mobileTemplateService = mobileTemplateService;
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

        _ = ProcessTickAsync().ContinueWith(
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
                dueStates = _states.Values
                                  .Where(state => state.NextSpawnAt <= now)
                                  .Select(state => state with { })
                                  .ToList();
            }

            foreach (var state in dueStates)
            {
                await TrySpawnForStateAsync(state, now);
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
                    string.IsNullOrWhiteSpace(spawnerIdRaw) ||
                    !Guid.TryParse(spawnerIdRaw, out var spawnerGuid))
                {
                    continue;
                }

                if (!_definitionsByGuid.TryGetValue(spawnerGuid, out var definition))
                {
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

    private async Task TrySpawnForStateAsync(RuntimeSpawnerState state, long now)
    {
        if (_spatialWorldService.GetPlayersInRange(state.Location, ActivationRange, state.MapId).Count == 0)
        {
            Reschedule(state.SpawnerItemId, state.Definition, now);

            return;
        }

        var currentCount = _spatialWorldService.GetNearbyMobiles(
                                            state.Location,
                                            Math.Max(1, state.Definition.HomeRange),
                                            state.MapId
                                        )
                                        .Count(
                                            mobile => !mobile.IsPlayer &&
                                                      mobile.TryGetCustomString(SpawnOriginKey, out var origin) &&
                                                      string.Equals(
                                                          origin,
                                                          state.SpawnGuid.ToString("D"),
                                                          StringComparison.OrdinalIgnoreCase
                                                      )
                                        );

        if (currentCount >= Math.Max(1, state.Definition.Count))
        {
            Reschedule(state.SpawnerItemId, state.Definition, now);

            return;
        }

        var templateId = ResolveTemplateId(state.Definition.Entries);

        if (templateId is null)
        {
            Reschedule(state.SpawnerItemId, state.Definition, now);

            return;
        }

        try
        {
            var mobile = await _mobileService.SpawnFromTemplateAsync(
                             templateId,
                             state.Location,
                             state.MapId,
                             accountId: null
                         );
            mobile.SetCustomString(SpawnOriginKey, state.SpawnGuid.ToString("D"));
            await _mobileService.CreateOrUpdateAsync(mobile);
            _spatialWorldService.AddOrUpdateMobile(mobile);

            _logger.Debug(
                "Spawned mobile from spawner {SpawnerGuid}: MobileId={MobileId} Template={TemplateId}",
                state.SpawnGuid,
                mobile.Id,
                templateId
            );
        }
        catch (Exception ex)
        {
            _logger.Warning(
                ex,
                "Failed to spawn mobile from spawner {SpawnerGuid} using template {TemplateId}.",
                state.SpawnGuid,
                templateId
            );
        }
        finally
        {
            Reschedule(state.SpawnerItemId, state.Definition, now);
        }
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

    private string? ResolveTemplateId(IReadOnlyList<SpawnEntryDefinition> entries)
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

            if (resolved is not null)
            {
                return resolved;
            }
        }

        foreach (var entry in weighted)
        {
            var resolved = ResolveTemplateId(entry.Name);

            if (resolved is not null)
            {
                return resolved;
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
}
