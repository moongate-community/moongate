using System.Diagnostics;
using Moongate.Abstractions.Services.Base;
using Moongate.Core.Data.Directories;
using Moongate.Scripting.Interfaces;
using Moongate.Scripting.Services;
using Moongate.Server.Attributes;
using Moongate.Server.Data.Config;
using Moongate.Server.Data.Events.Spatial;
using Moongate.Server.Data.Events.Speech;
using Moongate.Server.Data.Internal.Scripting;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Metrics;
using Moongate.Server.Interfaces.Services.Scripting;
using Moongate.Server.Interfaces.Services.Timing;
using Moongate.Server.Metrics.Data;
using Moongate.Server.Services.Scripting.Internal;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using MoonSharp.Interpreter;
using Serilog;

namespace Moongate.Server.Services.Scripting;

/// <summary>
/// Minimal tactical runner for Lua-driven NPC brains.
/// </summary>
[RegisterGameEventListener]
public sealed class LuaBrainRunner
    : BaseMoongateService,
      ILuaBrainRunner,
      ILuaBrainMetricsSource,
      IGameEventListener<SpeechHeardEvent>,
      IGameEventListener<MobileAddedInWorldEvent>,
      IGameEventListener<MobilePositionChangedEvent>,
      IGameEventListener<MobileSpawnedFromSpawnerEvent>
{
    private const int DefaultInRangeEnterDistance = 3;
    private const int RangedGuardInRangeEnterDistance = 10;
    private const int DefaultTickMilliseconds = 250;
    private const int FaultRetryMilliseconds = 1000;

    private readonly ITimerService _timerService;
    private readonly IScriptEngineService _scriptEngineService;
    private readonly ILuaBrainRegistry _luaBrainRegistry;
    private readonly ILogger _logger = Log.ForContext<LuaBrainRunner>();
    private readonly Script? _luaScript;
    private readonly int _maxBrainsPerTick;
    private readonly LuaBrainStateStore _stateStore = new();
    private readonly LuaBrainMetricsTracker _metricsTracker = new();

    private string? _timerId;

    public LuaBrainRunner(
        ITimerService timerService,
        IScriptEngineService scriptEngineService,
        ILuaBrainRegistry luaBrainRegistry,
        DirectoriesConfig directoriesConfig,
        MoongateConfig? config = null
    )
    {
        _timerService = timerService;
        _scriptEngineService = scriptEngineService;
        _luaBrainRegistry = luaBrainRegistry;
        _ = directoriesConfig;
        _luaScript = (scriptEngineService as LuaScriptEngineService)?.LuaScript;

        var configuredMaxBrains = config?.Scripting?.LuaBrainMaxBrainsPerTick ?? 0;
        _maxBrainsPerTick = configuredMaxBrains <= 0 ? int.MaxValue : configuredMaxBrains;
    }

    /// <inheritdoc />
    public void EnqueueCombatHook(Serial mobileId, LuaBrainCombatHookContext combatContext)
        => _stateStore.EnqueueCombatHook(mobileId, combatContext);

    /// <inheritdoc />
    public void EnqueueDeath(Serial mobileId, LuaBrainDeathContext deathContext)
        => _stateStore.EnqueueDeath(mobileId, deathContext);

    /// <inheritdoc />
    public void EnqueueInRange(Serial listenerNpcId, UOMobileEntity sourceMobile, int range = DefaultInRangeEnterDistance)
        => _stateStore.EnqueueInRange(listenerNpcId, sourceMobile, range);

    /// <inheritdoc />
    public void EnqueueSpawn(MobileSpawnedFromSpawnerEvent gameEvent)
        => _stateStore.EnqueueSpawn(gameEvent);

    /// <inheritdoc />
    public void EnqueueSpeech(SpeechHeardEvent gameEvent)
        => _stateStore.EnqueueSpeech(gameEvent);

    /// <inheritdoc />
    public IReadOnlyList<LuaBrainContextMenuEntry> GetContextMenuEntries(UOMobileEntity mobile, UOMobileEntity? requester)
    {
        ArgumentNullException.ThrowIfNull(mobile);

        if (!_stateStore.TryGet(mobile.Id, out var state) || state is null)
        {
            return [];
        }

        return LuaBrainContextMenuDispatcher.GetEntries(_luaScript, state, requester, _logger);
    }

    /// <inheritdoc />
    public LuaBrainMetricsSnapshot GetMetricsSnapshot()
        => _metricsTracker.CreateSnapshot();

    /// <inheritdoc />
    public Task HandleAsync(SpeechHeardEvent gameEvent, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        EnqueueSpeech(gameEvent);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task HandleAsync(MobileAddedInWorldEvent gameEvent, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        _stateStore.UpsertObservedMobile(gameEvent.Mobile);
        NotifyInRangeForAddedMobile(gameEvent.Mobile);

        if (gameEvent.Mobile.IsPlayer)
        {
            return Task.CompletedTask;
        }

        var resolvedBrainId = gameEvent.BrainId;

        if (string.IsNullOrWhiteSpace(resolvedBrainId))
        {
            resolvedBrainId = gameEvent.Mobile.BrainId;
        }

        if (string.IsNullOrWhiteSpace(resolvedBrainId))
        {
            return Task.CompletedTask;
        }

        Register(gameEvent.Mobile, resolvedBrainId);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task HandleAsync(MobileSpawnedFromSpawnerEvent gameEvent, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        EnqueueSpawn(gameEvent);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task HandleAsync(MobilePositionChangedEvent gameEvent, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        _stateStore.UpdateTrackedMobilePosition(gameEvent.MobileId, gameEvent.MapId, gameEvent.NewLocation);
        NotifyInRangeForMovedMobile(gameEvent);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Register(UOMobileEntity mobile, string brainId)
    {
        ArgumentNullException.ThrowIfNull(mobile);
        ArgumentException.ThrowIfNullOrWhiteSpace(brainId);

        if (mobile.IsPlayer)
        {
            return;
        }

        if (string.Equals(brainId.Trim(), "none", StringComparison.OrdinalIgnoreCase))
        {
            Unregister(mobile.Id);

            return;
        }

        var normalizedBrainId = brainId.Trim();
        var brainTableName = ResolveBrainTableName(normalizedBrainId);

        if (_stateStore.TryGet(mobile.Id, out var state) && state is not null)
        {
            state.Mobile = mobile;
            state.BrainId = normalizedBrainId;
            state.BrainTableName = brainTableName;
            state.AiNextWakeTime = 0;
            state.IsFaulted = false;
            state.PendingSpeech.Clear();
            state.PendingDeath.Clear();
            state.PendingSpawn.Clear();
            state.PendingCombatHooks.Clear();
            state.PendingInRange.Clear();
            state.PendingOutRange.Clear();
            LuaBrainLifecycle.InitializeRuntimeState(_luaScript, state, _logger);

            return;
        }

        var runtimeState = new LuaBrainRuntimeState(mobile, normalizedBrainId, brainTableName);
        LuaBrainLifecycle.InitializeRuntimeState(_luaScript, runtimeState, _logger);
        _stateStore.Upsert(runtimeState);
    }

    /// <inheritdoc />
    public override Task StartAsync()
    {
        if (!string.IsNullOrWhiteSpace(_timerId))
        {
            return Task.CompletedTask;
        }

        _timerId = _timerService.RegisterTimer(
            "lua_brain_runner",
            TimeSpan.FromMilliseconds(DefaultTickMilliseconds),
            TickCallback,
            repeat: true
        );

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task StopAsync()
    {
        if (!string.IsNullOrWhiteSpace(_timerId))
        {
            _timerService.UnregisterTimer(_timerId);
            _timerId = null;
        }

        _stateStore.Clear();

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask TickAllAsync(long nowMilliseconds, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        var tickStart = Stopwatch.GetTimestamp();

        var dueStates = _stateStore.GetDueStates(nowMilliseconds);
        var dueCount = dueStates.Count;

        if (_maxBrainsPerTick != int.MaxValue && dueStates.Count > _maxBrainsPerTick)
        {
            dueStates = dueStates.OrderBy(static state => state.AiNextWakeTime)
                                 .ThenBy(static state => (uint)state.MobileId)
                                 .Take(_maxBrainsPerTick)
                                 .ToList();
        }

        foreach (var state in dueStates)
        {
            var nextWake = nowMilliseconds + DefaultTickMilliseconds;

            try
            {
                if (state.IsFaulted)
                {
                    nextWake = LuaBrainFaultPolicy.NextWakeAfterFault(nowMilliseconds, FaultRetryMilliseconds);
                }
                else if (_luaScript is not null)
                {
                    nextWake = LuaBrainTickExecutor.Tick(
                        nowMilliseconds,
                        _luaScript,
                        state,
                        DefaultTickMilliseconds,
                        FaultRetryMilliseconds,
                        Unregister
                    );
                }
                else
                {
                    LuaBrainFallbackDispatcher.DispatchAll(_scriptEngineService, state);
                }
            }
            catch (Exception ex)
            {
                state.IsFaulted = true;
                nextWake = LuaBrainFaultPolicy.NextWakeAfterFault(nowMilliseconds, FaultRetryMilliseconds);
                _logger.Error(ex, "Lua brain tick failed for mobile {MobileId}", state.MobileId);
            }

            _stateStore.UpdateWakeTime(state.MobileId, nextWake);
        }

        var elapsedMilliseconds = Stopwatch.GetElapsedTime(tickStart).TotalMilliseconds;
        _metricsTracker.RecordTick(dueCount, dueStates.Count, elapsedMilliseconds);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public bool TryHandleContextMenuSelection(
        UOMobileEntity mobile,
        UOMobileEntity? requester,
        string menuKey,
        long sessionId
    )
    {
        ArgumentNullException.ThrowIfNull(mobile);

        if (string.IsNullOrWhiteSpace(menuKey) || !_stateStore.TryGet(mobile.Id, out var state) || state is null)
        {
            return false;
        }

        return LuaBrainContextMenuDispatcher.TryHandleSelection(
            _luaScript,
            state,
            requester,
            menuKey,
            sessionId,
            _logger
        );
    }

    /// <inheritdoc />
    public void Unregister(Serial mobileId)
        => _stateStore.Remove(mobileId);

    private void NotifyInRangeForAddedMobile(UOMobileEntity sourceMobile)
    {
        var snapshot = _stateStore.GetAllStates();

        foreach (var state in snapshot)
        {
            if (state.MobileId == sourceMobile.Id || state.Mobile.MapId != sourceMobile.MapId)
            {
                continue;
            }

            var acquisitionRange = ResolveAcquisitionRange(state.Mobile);

            if (!state.Mobile.Location.InRange(sourceMobile.Location, acquisitionRange))
            {
                continue;
            }

            _stateStore.EnqueueInRange(state.MobileId, sourceMobile, acquisitionRange);
        }
    }

    private void NotifyInRangeForMovedMobile(MobilePositionChangedEvent gameEvent)
    {
        if (!_stateStore.TryResolveTrackedMobile(gameEvent.MobileId, out var sourceMobile) ||
            sourceMobile is null)
        {
            return;
        }

        var snapshot = _stateStore.GetAllStates();

        foreach (var state in snapshot)
        {
            if (state.MobileId == sourceMobile.Id)
            {
                continue;
            }

            var acquisitionRange = ResolveAcquisitionRange(state.Mobile);
            var isInRangeNow = state.Mobile.MapId == gameEvent.MapId &&
                               state.Mobile.Location.InRange(gameEvent.NewLocation, acquisitionRange);
            var wasInRangeBefore = state.Mobile.MapId == gameEvent.OldMapId &&
                                   state.Mobile.Location.InRange(gameEvent.OldLocation, acquisitionRange);

            if (!wasInRangeBefore && isInRangeNow)
            {
                _stateStore.EnqueueInRange(state.MobileId, sourceMobile, acquisitionRange);
            }

            if (wasInRangeBefore && !isInRangeNow)
            {
                _stateStore.EnqueueOutRange(state.MobileId, sourceMobile, acquisitionRange);
            }
        }
    }

    private static int ResolveAcquisitionRange(UOMobileEntity mobile)
    {
        if (mobile.TryGetCustomString("guard_role", out var guardRole) &&
            string.Equals(guardRole, "ranged", StringComparison.OrdinalIgnoreCase))
        {
            return RangedGuardInRangeEnterDistance;
        }

        return DefaultInRangeEnterDistance;
    }

    private string ResolveBrainTableName(string brainId)
    {
        if (_luaBrainRegistry.TryGet(brainId, out var definition) && definition is not null)
        {
            return definition.BrainId.Trim();
        }

        return brainId;
    }

    private void TickCallback()
    {
        var nowMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        TickAllAsync(nowMilliseconds).AsTask().GetAwaiter().GetResult();
    }
}
