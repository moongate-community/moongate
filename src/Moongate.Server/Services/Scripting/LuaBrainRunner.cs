using Moongate.Abstractions.Services.Base;
using Moongate.Core.Data.Directories;
using Moongate.Core.Types;
using Moongate.Scripting.Interfaces;
using Moongate.Scripting.Services;
using Moongate.Server.Attributes;
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
    : BaseMoongateService, ILuaBrainRunner, IGameEventListener<SpeechHeardEvent>
{
    private const int DefaultTickMilliseconds = 250;
    private const int FaultRetryMilliseconds = 1000;
    private readonly Dictionary<Serial, LuaBrainRuntimeState> _states = [];
    private readonly HashSet<string> _loadedScriptPaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _syncRoot = new();
    private readonly ITimerService _timerService;
    private readonly IScriptEngineService _scriptEngineService;
    private readonly ILuaBrainRegistry _luaBrainRegistry;
    private readonly DirectoriesConfig _directoriesConfig;
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
        _directoriesConfig = directoriesConfig;
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
            _loadedScriptPaths.Clear();
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

        var scriptPath = ResolveScriptPath(brainId.Trim());

        if (scriptPath is null)
        {
            _logger.Warning(
                "Cannot register lua brain {BrainId} for mobile {MobileId}: script path cannot be resolved.",
                brainId,
                mobile.Id
            );
            Unregister(mobile.Id);

            return;
        }

        lock (_syncRoot)
        {
            if (_states.TryGetValue(mobile.Id, out var state))
            {
                state.Mobile = mobile;
                state.BrainId = brainId.Trim();
                state.ScriptPath = scriptPath;
                state.AiNextWakeTime = 0;
                state.IsFaulted = false;
                state.PendingSpeech.Clear();
                InitializeRuntimeState(state);

                return;
            }

            var runtimeState = new LuaBrainRuntimeState(mobile, brainId.Trim(), scriptPath);
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
    public ValueTask TickAllAsync(long nowMilliseconds, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        List<LuaBrainRuntimeState> dueStates;

        lock (_syncRoot)
        {
            dueStates = _states.Values
                              .Where(state => nowMilliseconds >= state.AiNextWakeTime)
                              .Select(static state => state)
                              .ToList();
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

    private void EnsureScriptLoaded(string scriptPath)
    {
        if (_loadedScriptPaths.Contains(scriptPath))
        {
            return;
        }

        _scriptEngineService.ExecuteScriptFile(scriptPath);
        _loadedScriptPaths.Add(scriptPath);
    }

    private void InitializeRuntimeState(LuaBrainRuntimeState state)
    {
        EnsureScriptLoaded(state.ScriptPath);

        if (!_supportsMoonSharpRuntime || _luaScript is null)
        {
            return;
        }

        var onSpeechFunction = ResolveScriptFunction("on_speech", "OnSpeech");
        state.OnSpeechFunction = onSpeechFunction?.Type == DataType.Function ? onSpeechFunction : null;

        var brainLoop = ResolveScriptFunction("brain_loop", "BrainLoop", "on_brain_tick", "OnBrainTick");

        if (brainLoop is null || brainLoop.Type != DataType.Function)
        {
            _logger.Warning(
                "Brain script for mobile {MobileId} does not expose brain loop function. Expected brain_loop/BrainLoop.",
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
        if (_luaScript is null || state.OnSpeechFunction is null)
        {
            state.PendingSpeech.Clear();

            return;
        }

        while (state.PendingSpeech.Count > 0)
        {
            var speech = state.PendingSpeech.Dequeue();
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

    private int ParseYieldDelay(DynValue yielded)
    {
        if (yielded.Type == DataType.Number)
        {
            var value = (int)Math.Round(yielded.Number);

            return value <= 0 ? DefaultTickMilliseconds : value;
        }

        return DefaultTickMilliseconds;
    }

    private DynValue? ResolveScriptFunction(params string[] functionNames)
    {
        if (_luaScript is null)
        {
            return null;
        }

        foreach (var functionName in functionNames)
        {
            var function = _luaScript.Globals.Get(functionName);

            if (function.Type == DataType.Function)
            {
                return function;
            }
        }

        return null;
    }

    private string? ResolveScriptPath(string brainId)
    {
        if (_luaBrainRegistry.TryGet(brainId, out var definition) && definition is not null)
        {
            return ResolveScriptPathCore(definition.ScriptPath);
        }

        return ResolveScriptPathCore(brainId);
    }

    private string? ResolveScriptPathCore(string scriptPathOrBrainId)
    {
        if (string.IsNullOrWhiteSpace(scriptPathOrBrainId))
        {
            return null;
        }

        var resolvedPath = scriptPathOrBrainId.Trim();

        if (!resolvedPath.Contains('/') && !resolvedPath.Contains('\\') && !resolvedPath.EndsWith(".lua", StringComparison.OrdinalIgnoreCase))
        {
            resolvedPath = $"ai/{resolvedPath}.lua";
        }
        else if (!resolvedPath.EndsWith(".lua", StringComparison.OrdinalIgnoreCase))
        {
            resolvedPath = $"{resolvedPath}.lua";
        }

        if (resolvedPath.StartsWith("scripts/", StringComparison.OrdinalIgnoreCase))
        {
            resolvedPath = resolvedPath["scripts/".Length..];
        }

        if (!Path.IsPathRooted(resolvedPath))
        {
            resolvedPath = Path.Combine(_directoriesConfig[DirectoryType.Scripts], resolvedPath);
        }

        if (!File.Exists(resolvedPath))
        {
            _logger.Warning("Lua brain script not found at {ScriptPath}", resolvedPath);

            return null;
        }

        return resolvedPath;
    }
}
