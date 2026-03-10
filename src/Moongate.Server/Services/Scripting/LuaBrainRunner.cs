using Moongate.Abstractions.Services.Base;
using Moongate.Core.Data.Directories;
using Moongate.Scripting.Interfaces;
using Moongate.Scripting.Services;
using Moongate.Server.Attributes;
using Moongate.Server.Data.Events.Spatial;
using Moongate.Server.Data.Events.Speech;
using Moongate.Server.Data.Internal.Scripting;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Scripting;
using Moongate.Server.Interfaces.Services.Timing;
using Moongate.Server.Services.Scripting.Internal;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Geometry;
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
      IGameEventListener<SpeechHeardEvent>,
      IGameEventListener<MobileAddedInWorldEvent>,
      IGameEventListener<MobilePositionChangedEvent>,
      IGameEventListener<MobileSpawnedFromSpawnerEvent>
{
    private const int InRangeEnterDistance = 3;
    private const int DefaultTickMilliseconds = 250;
    private const int FaultRetryMilliseconds = 1000;
    private readonly Dictionary<Serial, LuaBrainRuntimeState> _states = [];
    private readonly Lock _syncRoot = new();
    private readonly ITimerService _timerService;
    private readonly IScriptEngineService _scriptEngineService;
    private readonly ILuaBrainRegistry _luaBrainRegistry;
    private readonly ILogger _logger = Log.ForContext<LuaBrainRunner>();
    private readonly Script? _luaScript;

    private string? _timerId;

    public LuaBrainRunner(
        ITimerService timerService,
        IScriptEngineService scriptEngineService,
        ILuaBrainRegistry luaBrainRegistry,
        DirectoriesConfig directoriesConfig
    )
    {
        _timerService = timerService;
        _scriptEngineService = scriptEngineService;
        _luaBrainRegistry = luaBrainRegistry;
        _ = directoriesConfig;
        _luaScript = (scriptEngineService as LuaScriptEngineService)?.LuaScript;
    }

    /// <inheritdoc />
    public void EnqueueDeath(Serial mobileId, LuaBrainDeathContext deathContext)
    {
        lock (_syncRoot)
        {
            if (_states.TryGetValue(mobileId, out var state))
            {
                state.PendingDeath.Enqueue(deathContext);
            }
        }
    }

    /// <inheritdoc />
    public void EnqueueSpeech(SpeechHeardEvent gameEvent)
    {
        lock (_syncRoot)
        {
            if (_states.TryGetValue(gameEvent.ListenerNpcId, out var state))
            {
                state.PendingSpeech.Enqueue(gameEvent);
            }
        }
    }

    /// <inheritdoc />
    public void EnqueueSpawn(MobileSpawnedFromSpawnerEvent gameEvent)
    {
        lock (_syncRoot)
        {
            if (_states.TryGetValue(gameEvent.Mobile.Id, out var state))
            {
                state.PendingSpawn.Enqueue(gameEvent);
            }
        }
    }

    /// <inheritdoc />
    public void EnqueueInRange(Serial listenerNpcId, UOMobileEntity sourceMobile, int range = InRangeEnterDistance)
    {
        lock (_syncRoot)
        {
            if (!_states.TryGetValue(listenerNpcId, out var state))
            {
                return;
            }

            state.PendingInRange.Enqueue(
                new LuaBrainInRangeContext(
                    sourceMobile.Id,
                    BuildInRangeEventPayload(state.MobileId, sourceMobile, range)
                )
            );
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<LuaBrainContextMenuEntry> GetContextMenuEntries(UOMobileEntity mobile, UOMobileEntity? requester)
    {
        ArgumentNullException.ThrowIfNull(mobile);

        if (_luaScript is null)
        {
            return [];
        }

        LuaBrainRuntimeState? state;

        lock (_syncRoot)
        {
            if (!_states.TryGetValue(mobile.Id, out state))
            {
                return [];
            }
        }

        if (state.OnGetContextMenusFunction is null)
        {
            return [];
        }

        try
        {
            var payload = LuaBrainContextMenuPayloadFactory.Build(
                new LuaBrainContextMenuPayload(
                    state.MobileId,
                    requester,
                    0,
                    null
                )
            );
            var result = _luaScript.Call(state.OnGetContextMenusFunction, payload);

            return LuaBrainContextMenuParser.Parse(result);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Lua get_context_menus failed for mobile {MobileId}", mobile.Id);

            return [];
        }
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

        if (string.IsNullOrWhiteSpace(menuKey) || _luaScript is null)
        {
            return false;
        }

        LuaBrainRuntimeState? state;

        lock (_syncRoot)
        {
            if (!_states.TryGetValue(mobile.Id, out state))
            {
                return false;
            }
        }

        var payload = LuaBrainContextMenuPayloadFactory.Build(
            new LuaBrainContextMenuPayload(
                state.MobileId,
                requester,
                sessionId,
                menuKey
            )
        );

        try
        {
            if (state.OnSelectedContextMenuFunction is not null)
            {
                _luaScript.Call(state.OnSelectedContextMenuFunction, menuKey, payload);

                return true;
            }

            if (state.OnEventFunction is not null)
            {
                var requesterId = requester is null ? 0u : (uint)requester.Id;
                _luaScript.Call(state.OnEventFunction, "context_menu_selected", requesterId, payload);

                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Lua on_selected_context_menu failed for mobile {MobileId} key {MenuKey}", mobile.Id, menuKey);
        }

        return false;
    }

    private void EnqueueOutRange(Serial listenerNpcId, UOMobileEntity sourceMobile, int range = InRangeEnterDistance)
    {
        lock (_syncRoot)
        {
            if (!_states.TryGetValue(listenerNpcId, out var state))
            {
                return;
            }

            state.PendingOutRange.Enqueue(
                new LuaBrainInRangeContext(
                    sourceMobile.Id,
                    BuildInRangeEventPayload(state.MobileId, sourceMobile, range)
                )
            );
        }
    }

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
        lock (_syncRoot)
        {
            if (_states.TryGetValue(gameEvent.MobileId, out var trackedState))
            {
                trackedState.Mobile.MapId = gameEvent.MapId;
                trackedState.Mobile.Location = gameEvent.NewLocation;
            }
        }

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

        lock (_syncRoot)
        {
            if (_states.TryGetValue(mobile.Id, out var state))
            {
                state.Mobile = mobile;
                state.BrainId = normalizedBrainId;
                state.BrainTableName = brainTableName;
                state.AiNextWakeTime = 0;
                state.IsFaulted = false;
                state.PendingSpeech.Clear();
                state.PendingDeath.Clear();
                state.PendingSpawn.Clear();
                state.PendingInRange.Clear();
                state.PendingOutRange.Clear();
                InitializeRuntimeState(state);

                return;
            }

            var runtimeState = new LuaBrainRuntimeState(mobile, normalizedBrainId, brainTableName);
            InitializeRuntimeState(runtimeState);
            _states[mobile.Id] = runtimeState;
        }
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

        lock (_syncRoot)
        {
            _states.Clear();
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask TickAllAsync(long nowMilliseconds, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        List<LuaBrainRuntimeState> dueStates;

        lock (_syncRoot)
        {
            dueStates =
            [
                .. _states.Values
                          .Where(state => nowMilliseconds >= state.AiNextWakeTime)
                          .Select(static state => state)
            ];
        }

        foreach (var state in dueStates)
        {
            var nextWake = nowMilliseconds + DefaultTickMilliseconds;

            try
            {
                if (state.IsFaulted)
                {
                    // TODO: Brain fallback behavior (point 5 excluded for now).
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
                    TickFallbackState(state);
                }
            }
            catch (Exception ex)
            {
                state.IsFaulted = true;
                nextWake = LuaBrainFaultPolicy.NextWakeAfterFault(nowMilliseconds, FaultRetryMilliseconds);
                _logger.Error(ex, "Lua brain tick failed for mobile {MobileId}", state.MobileId);
            }

            lock (_syncRoot)
            {
                if (_states.TryGetValue(state.MobileId, out var trackedState))
                {
                    trackedState.AiNextWakeTime = nextWake;
                }
            }
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public void Unregister(Serial mobileId)
    {
        lock (_syncRoot)
        {
            _states.Remove(mobileId);
        }
    }

    private static Dictionary<string, object> BuildSpeechEventPayload(SpeechHeardEvent speech)
        => new()
        {
            ["listener_npc_id"] = (uint)speech.ListenerNpcId,
            ["speaker_id"] = (uint)speech.SpeakerId,
            ["text"] = speech.Text,
            ["speech_type"] = (byte)speech.SpeechType,
            ["map_id"] = speech.MapId,
            ["location"] = new Dictionary<string, int>
            {
                ["x"] = speech.Location.X,
                ["y"] = speech.Location.Y,
                ["z"] = speech.Location.Z
            }
        };

    private static Dictionary<string, object> BuildSpawnEventPayload(MobileSpawnedFromSpawnerEvent spawn)
        => new()
        {
            ["mobile_id"] = (uint)spawn.Mobile.Id,
            ["spawner_guid"] = spawn.SpawnerGuid.ToString("D"),
            ["spawner_name"] = spawn.SpawnerName,
            ["source_group"] = spawn.SourceGroup,
            ["source_file"] = spawn.SourceFile,
            ["spawn_count"] = spawn.SpawnCount,
            ["min_delay_ms"] = (int)spawn.MinDelay.TotalMilliseconds,
            ["max_delay_ms"] = (int)spawn.MaxDelay.TotalMilliseconds,
            ["team"] = spawn.Team,
            ["home_range"] = spawn.HomeRange,
            ["walking_range"] = spawn.WalkingRange,
            ["entry_name"] = spawn.EntryName,
            ["entry_max_count"] = spawn.EntryMaxCount,
            ["entry_probability"] = spawn.EntryProbability,
            ["map_id"] = spawn.Mobile.MapId,
            ["location"] = new Dictionary<string, int>
            {
                ["x"] = spawn.Mobile.Location.X,
                ["y"] = spawn.Mobile.Location.Y,
                ["z"] = spawn.Mobile.Location.Z
            },
            ["spawner_location"] = new Dictionary<string, int>
            {
                ["x"] = spawn.SpawnerLocation.X,
                ["y"] = spawn.SpawnerLocation.Y,
                ["z"] = spawn.SpawnerLocation.Z
            }
        };

    private static Dictionary<string, object> BuildInRangeEventPayload(
        Serial listenerNpcId,
        UOMobileEntity sourceMobile,
        int range
    )
        => new()
        {
            ["listener_npc_id"] = (uint)listenerNpcId,
            ["source_mobile_id"] = (uint)sourceMobile.Id,
            ["source_is_player"] = sourceMobile.IsPlayer,
            ["map_id"] = sourceMobile.MapId,
            ["range"] = range,
            ["location"] = new Dictionary<string, int>
            {
                ["x"] = sourceMobile.Location.X,
                ["y"] = sourceMobile.Location.Y,
                ["z"] = sourceMobile.Location.Z
            }
        };

    private void InitializeRuntimeState(LuaBrainRuntimeState state)
    {
        if (_luaScript is null)
        {
            return;
        }

        if (!LuaBrainHookBinder.TryBind(_luaScript, state.BrainTableName, out var hooks))
        {
            _logger.Warning(
                "Lua brain table {BrainTable} not found for mobile {MobileId}.",
                state.BrainTableName,
                state.MobileId
            );
            state.BrainCoroutine = null;
            state.OnEventFunction = null;
            state.OnSpeechFunction = null;
            state.OnDeathFunction = null;
            state.OnSpawnFunction = null;
            state.OnInRangeFunction = null;
            state.OnOutRangeFunction = null;
            state.OnGetContextMenusFunction = null;
            state.OnSelectedContextMenuFunction = null;
            state.IsFaulted = true;

            return;
        }

        state.OnSpeechFunction = hooks.OnSpeechFunction;
        state.OnDeathFunction = hooks.OnDeathFunction;
        state.OnSpawnFunction = hooks.OnSpawnFunction;
        state.OnInRangeFunction = hooks.OnInRangeFunction;
        state.OnOutRangeFunction = hooks.OnOutRangeFunction;
        state.OnGetContextMenusFunction = hooks.OnGetContextMenusFunction;
        state.OnSelectedContextMenuFunction = hooks.OnSelectedContextMenuFunction;
        state.OnEventFunction = hooks.OnEventFunction;

        var brainLoop = hooks.BrainLoopFunction;

        if (brainLoop is null || brainLoop.Type != DataType.Function)
        {
            _logger.Warning(
                "Lua brain table {BrainTable} for mobile {MobileId} does not expose brain_loop.",
                state.BrainTableName,
                state.MobileId
            );

            state.BrainCoroutine = null;
            state.IsFaulted = true;

            return;
        }

        state.BrainCoroutine = _luaScript.CreateCoroutine(brainLoop).Coroutine;
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

    private void TickFallbackState(LuaBrainRuntimeState state)
    {
        while (state.PendingSpawn.Count > 0)
        {
            var spawn = state.PendingSpawn.Dequeue();
            var payload = BuildSpawnEventPayload(spawn);

            _scriptEngineService.CallFunction(
                "on_spawn",
                (uint)state.MobileId,
                payload
            );
            _scriptEngineService.CallFunction(
                "on_event",
                "spawn",
                0u,
                payload
            );
        }

        while (state.PendingSpeech.Count > 0)
        {
            var speech = state.PendingSpeech.Dequeue();
            _scriptEngineService.CallFunction(
                "on_event",
                "speech_heard",
                (uint)speech.SpeakerId,
                BuildSpeechEventPayload(speech)
            );

            _scriptEngineService.CallFunction(
                "on_speech",
                (uint)speech.ListenerNpcId,
                (uint)speech.SpeakerId,
                speech.Text,
                (byte)speech.SpeechType,
                speech.MapId,
                speech.Location.X,
                speech.Location.Y,
                speech.Location.Z
            );
        }

        while (state.PendingDeath.Count > 0)
        {
            var death = state.PendingDeath.Dequeue();
            var byCharacterId = death.ByCharacterId.HasValue ? (uint)death.ByCharacterId.Value : 0u;

            _scriptEngineService.CallFunction(
                "on_event",
                "death",
                byCharacterId,
                death.Context
            );
            _scriptEngineService.CallFunction(
                "on_death",
                byCharacterId,
                death.Context
            );
        }

        while (state.PendingInRange.Count > 0)
        {
            var inRange = state.PendingInRange.Dequeue();
            _scriptEngineService.CallFunction(
                "on_event",
                "in_range",
                (uint)inRange.SourceMobileId,
                inRange.Payload
            );
            _scriptEngineService.CallFunction(
                "on_in_range",
                (uint)state.MobileId,
                (uint)inRange.SourceMobileId,
                inRange.Payload
            );
        }

        while (state.PendingOutRange.Count > 0)
        {
            var outRange = state.PendingOutRange.Dequeue();
            _scriptEngineService.CallFunction(
                "on_event",
                "out_range",
                (uint)outRange.SourceMobileId,
                outRange.Payload
            );
            _scriptEngineService.CallFunction(
                "on_out_range",
                (uint)state.MobileId,
                (uint)outRange.SourceMobileId,
                outRange.Payload
            );
        }

        _scriptEngineService.CallFunction("on_brain_tick", (uint)state.MobileId);
    }

    private void NotifyInRangeForAddedMobile(UOMobileEntity sourceMobile)
    {
        if (!TryResolveSourceMobile(sourceMobile.Id, sourceMobile.MapId, sourceMobile.Location, out var resolvedSourceMobile))
        {
            return;
        }

        List<LuaBrainRuntimeState> snapshot;
        lock (_syncRoot)
        {
            snapshot = [.. _states.Values];
        }

        foreach (var state in snapshot)
        {
            if (state.MobileId == resolvedSourceMobile.Id || state.Mobile.MapId != resolvedSourceMobile.MapId)
            {
                continue;
            }

            if (!state.Mobile.Location.InRange(resolvedSourceMobile.Location, InRangeEnterDistance))
            {
                continue;
            }

            EnqueueInRange(state.MobileId, resolvedSourceMobile, InRangeEnterDistance);
        }
    }

    private void NotifyInRangeForMovedMobile(MobilePositionChangedEvent gameEvent)
    {
        if (!TryResolveSourceMobile(gameEvent.MobileId, gameEvent.MapId, gameEvent.NewLocation, out var sourceMobile))
        {
            return;
        }

        List<LuaBrainRuntimeState> snapshot;
        lock (_syncRoot)
        {
            snapshot = [.. _states.Values];
        }

        foreach (var state in snapshot)
        {
            if (state.MobileId == sourceMobile.Id)
            {
                continue;
            }

            var isInRangeNow = state.Mobile.MapId == gameEvent.MapId &&
                               state.Mobile.Location.InRange(gameEvent.NewLocation, InRangeEnterDistance);
            var wasInRangeBefore = state.Mobile.MapId == gameEvent.OldMapId &&
                                   state.Mobile.Location.InRange(gameEvent.OldLocation, InRangeEnterDistance);

            if (!wasInRangeBefore && isInRangeNow)
            {
                EnqueueInRange(state.MobileId, sourceMobile, InRangeEnterDistance);
            }

            if (wasInRangeBefore && !isInRangeNow)
            {
                EnqueueOutRange(state.MobileId, sourceMobile, InRangeEnterDistance);
            }
        }
    }

    private bool TryResolveSourceMobile(Serial mobileId, int mapId, Point3D location, out UOMobileEntity? sourceMobile)
    {
        lock (_syncRoot)
        {
            if (_states.TryGetValue(mobileId, out var trackedState))
            {
                sourceMobile = trackedState.Mobile;

                return true;
            }
        }

        // Fallback without spatial query: enough for range notifications payload.
        sourceMobile = new()
        {
            Id = mobileId,
            MapId = mapId,
            Location = location,
            IsPlayer = true
        };

        return true;
    }
}
