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
    : BaseMoongateService, ILuaBrainRunner, IGameEventListener<SpeechHeardEvent>, IGameEventListener<MobileAddedInWorldEvent>
{
    private const int DefaultTickMilliseconds = 250;
    private const int FaultRetryMilliseconds = 1000;
    private readonly Dictionary<Serial, LuaBrainRuntimeState> _states = [];
    private readonly Lock _syncRoot = new();
    private readonly ITimerService _timerService;
    private readonly IScriptEngineService _scriptEngineService;
    private readonly ILuaBrainRegistry _luaBrainRegistry;
    private readonly ILogger _logger = Log.ForContext<LuaBrainRunner>();
    private readonly Script? _luaScript;
    private readonly bool _supportsMoonSharpRuntime;

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
        _supportsMoonSharpRuntime = _luaScript is not null;
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
                InitializeRuntimeState(state);

                return;
            }

            var runtimeState = new LuaBrainRuntimeState(mobile, normalizedBrainId, brainTableName);
            InitializeRuntimeState(runtimeState);
            _states[mobile.Id] = runtimeState;
        }
    }

    /// <inheritdoc />
    public void Unregister(Serial mobileId)
    {
        lock (_syncRoot)
        {
            _states.Remove(mobileId);
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
                    nextWake = nowMilliseconds + FaultRetryMilliseconds;
                }
                else if (_supportsMoonSharpRuntime)
                {
                    nextWake = TickMoonSharpState(nowMilliseconds, state);
                }
                else
                {
                    TickFallbackState(state);
                }
            }
            catch (Exception ex)
            {
                state.IsFaulted = true;
                nextWake = nowMilliseconds + FaultRetryMilliseconds;
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

    private void TickCallback()
    {
        var nowMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        TickAllAsync(nowMilliseconds).AsTask().GetAwaiter().GetResult();
    }

    private void InitializeRuntimeState(LuaBrainRuntimeState state)
    {
        if (!_supportsMoonSharpRuntime || _luaScript is null)
        {
            return;
        }

        var brainTable = ResolveBrainTable(state.BrainTableName);

        if (brainTable is null)
        {
            _logger.Warning(
                "Lua brain table {BrainTable} not found for mobile {MobileId}.",
                state.BrainTableName,
                state.MobileId
            );
            state.BrainCoroutine = null;
            state.OnEventFunction = null;
            state.OnSpeechFunction = null;
            state.IsFaulted = true;

            return;
        }

        state.OnSpeechFunction = ResolveTableFunction(brainTable, "on_speech", "OnSpeech");
        state.OnEventFunction = ResolveTableFunction(brainTable, "on_event", "OnEvent");

        var brainLoop = ResolveTableFunction(brainTable, "brain_loop", "BrainLoop", "on_brain_tick", "OnBrainTick");

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

    private long TickMoonSharpState(long nowMilliseconds, LuaBrainRuntimeState state)
    {
        if (_luaScript is null)
        {
            return nowMilliseconds + DefaultTickMilliseconds;
        }

        if (state.BrainCoroutine is null)
        {
            state.IsFaulted = true;

            return nowMilliseconds + FaultRetryMilliseconds;
        }

        DispatchPendingSpeech(state);

        if (state.BrainCoroutine.State == CoroutineState.Dead)
        {
            Unregister(state.MobileId);

            return nowMilliseconds + DefaultTickMilliseconds;
        }

        var result = state.BrainCoroutine.State == CoroutineState.NotStarted
                         ? state.BrainCoroutine.Resume((uint)state.MobileId)
                         : state.BrainCoroutine.Resume();
        var delay = ParseYieldDelay(result);

        if (state.BrainCoroutine.State == CoroutineState.Dead)
        {
            Unregister(state.MobileId);
        }

        return nowMilliseconds + delay;
    }

    private void TickFallbackState(LuaBrainRuntimeState state)
    {
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

        _scriptEngineService.CallFunction("on_brain_tick", (uint)state.MobileId);
    }

    private void DispatchPendingSpeech(LuaBrainRuntimeState state)
    {
        if (_luaScript is null)
        {
            state.PendingSpeech.Clear();

            return;
        }

        while (state.PendingSpeech.Count > 0)
        {
            var speech = state.PendingSpeech.Dequeue();

            if (state.OnEventFunction is not null)
            {
                _luaScript.Call(
                    state.OnEventFunction,
                    "speech_heard",
                    (uint)speech.SpeakerId,
                    BuildSpeechEventPayload(speech)
                );

                continue;
            }

            if (state.OnSpeechFunction is null)
            {
                continue;
            }

            _luaScript.Call(
                state.OnSpeechFunction,
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
    }

    private static Dictionary<string, object> BuildSpeechEventPayload(SpeechHeardEvent speech)
    {
        return new()
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
    }

    private int ParseYieldDelay(DynValue yielded)
    {
        if (yielded.Type == DataType.Number)
        {
            var value = (int)Math.Round(yielded.Number);

            return value <= 0 ? DefaultTickMilliseconds : value;
        }

        return DefaultTickMilliseconds;
    }

    private DynValue? ResolveBrainTable(string brainTableName)
    {
        if (_luaScript is null)
        {
            return null;
        }

        return _luaScript.Globals.Get(brainTableName) is { Type: DataType.Table } table ? table : null;
    }

    private static DynValue? ResolveTableFunction(DynValue table, params string[] functionNames)
    {
        if (table.Type != DataType.Table || table.Table is null)
        {
            return null;
        }

        foreach (var functionName in functionNames)
        {
            var function = table.Table.Get(functionName);

            if (function.Type == DataType.Function)
            {
                return function;
            }
        }

        return null;
    }

    private string ResolveBrainTableName(string brainId)
    {
        if (_luaBrainRegistry.TryGet(brainId, out var definition) && definition is not null)
        {
            return definition.BrainId.Trim();
        }

        return brainId;
    }
}
