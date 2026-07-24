using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Moongate.Core.Interfaces;
using Moongate.Core.Primitives;
using Moongate.Server.Abstractions.Data.AI;
using Moongate.Server.Abstractions.Data.Config;
using Moongate.Server.Abstractions.Data.Events;
using Moongate.Server.Abstractions.Interfaces.AI;
using Moongate.Server.Abstractions.Types;
using MoonSharp.Interpreter;
using Serilog;
using SquidStd.Abstractions.Interfaces.Services;
using SquidStd.Core.Directories;
using SquidStd.Core.Interfaces.Events;
using SquidStd.Scripting.Lua.Interfaces.Scripts;
using SquidStd.Scripting.Lua.Services;

namespace Moongate.Scripting.AI;

public sealed class LuaNpcBrainRuntime : INpcBrainRuntime, ISquidStdService, IDisposable
{
    private const int MaximumBrainFileBytes = 256 * 1024;
    private const int MaximumPerceptionRange = 64;
    private const int ReloadDebounceMilliseconds = 250;
    private static readonly string[] OptionalHookNames =
    [
        "on_activate",
        "on_deactivate",
        "on_speech_heard",
        "on_mobile_entered_range",
        "on_mobile_left_range",
        "on_mobile_moved",
        "on_attacked",
        "on_damage",
        "on_death"
    ];
    private static readonly Regex ValidBrainIdPattern = new(
        "^[a-z0-9][a-z0-9_-]*$",
        RegexOptions.CultureInvariant
    );
    private static readonly string[] AllowedGlobalNames =
    [
        "assert",
        "error",
        "ipairs",
        "next",
        "pairs",
        "pcall",
        "select",
        "tonumber",
        "tostring",
        "type",
        "xpcall"
    ];
    private static readonly string[] AllowedLibraryNames = ["math", "string", "table"];
    private readonly NpcAiAdvancedConfig _advanced;
    private readonly Dictionary<Serial, BrainBinding> _bindings = [];
    private readonly string _brainsDirectory;
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _debounces = new(
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal
    );
    private readonly Dictionary<string, LuaBrainDefinition> _definitions = new(StringComparer.Ordinal);
    private readonly IEventBus _eventBus;
    private readonly IGameLoopContext _gameLoop;
    private readonly ILogger _logger = Log.ForContext<LuaNpcBrainRuntime>();
    private readonly INpcAiMetrics _metrics;
    private readonly Script _script;
    private readonly TimeProvider _timeProvider;
    private readonly LuaBrainValueConverter _valueConverter;
    private readonly Func<string, FileSystemWatcher> _watcherFactory;

    private WatcherRegistration? _watcherRegistration;
    private int _watcherGeneration;
    private int _watching;

    public LuaNpcBrainRuntime(
        IScriptEngineService scriptEngineService,
        DirectoriesConfig directoriesConfig,
        MoongateConfig config,
        INpcAiMetrics metrics,
        IGameLoopContext gameLoop,
        IEventBus eventBus,
        TimeProvider timeProvider,
        Func<string, FileSystemWatcher>? watcherFactory = null
    )
    {
        if (scriptEngineService is not LuaScriptEngineService luaScriptEngineService)
        {
            throw new InvalidOperationException(
                $"{nameof(LuaNpcBrainRuntime)} requires the SquidStd Lua engine implementation."
            );
        }

        _advanced = config.NpcAi.Advanced;
        _brainsDirectory = Path.Combine(directoriesConfig.GetPath("scripts"), "brains");
        _eventBus = eventBus;
        _gameLoop = gameLoop;
        _metrics = metrics;
        _script = luaScriptEngineService.LuaScript;
        _timeProvider = timeProvider;
        _valueConverter = new(_script, _advanced);
        _watcherFactory = watcherFactory ?? CreateFileSystemWatcher;
    }

    public NpcBrainInvocationResult Invoke(
        Serial mobileId,
        NpcBrainHookType hook,
        BrainContext context,
        NpcBrainEvent? brainEvent = null
    )
    {
        var hookName = GetHookName(hook);

        if (!_bindings.TryGetValue(mobileId, out var binding) ||
            !_definitions.TryGetValue(binding.BrainId, out var definition))
        {
            return FailInvocation("<unbound>", mobileId, hookName, "No brain is bound to the mobile.");
        }

        if (!IsSupportedHook(hook))
        {
            return FailInvocation(
                definition.Descriptor.BrainId,
                mobileId,
                hookName,
                $"Unsupported brain hook value: {(int)hook}."
            );
        }

        var hookValue = definition.Strategy.Get(hookName);

        if (hookValue.Type is DataType.Nil or DataType.Void)
        {
            return NpcBrainInvocationResult.Succeeded(BrainDecision.Empty);
        }

        try
        {
            var contextTable = _valueConverter.ToContext(context);
            var eventTable = _valueConverter.ToEvent(brainEvent);
            var coroutineValue = _script.CreateCoroutine(hookValue);
            var coroutine = coroutineValue.Coroutine;
            coroutine.AutoYieldCounter = _advanced.InstructionBudget;

            var result = coroutine.Resume(
                DynValue.NewTable(contextTable),
                DynValue.NewTable(binding.State),
                eventTable is null ? DynValue.Nil : DynValue.NewTable(eventTable)
            );

            if (coroutine.State == CoroutineState.ForceSuspended)
            {
                return FailInvocation(
                    definition.Descriptor.BrainId,
                    mobileId,
                    hookName,
                    "Instruction budget exceeded.",
                    true
                );
            }

            if (coroutine.State != CoroutineState.Dead)
            {
                return FailInvocation(
                    definition.Descriptor.BrainId,
                    mobileId,
                    hookName,
                    "Brain hooks may not yield."
                );
            }

            if (result.Type is not DataType.Nil and not DataType.Void and not DataType.Table)
            {
                return FailInvocation(
                    definition.Descriptor.BrainId,
                    mobileId,
                    hookName,
                    $"Unsupported brain hook return type '{result.Type}'. Expected nil or table."
                );
            }

            return NpcBrainInvocationResult.Succeeded(_valueConverter.ToDecision(result));
        }
        catch (InterpreterException exception)
        {
            return FailInvocation(
                definition.Descriptor.BrainId,
                mobileId,
                hookName,
                GetInterpreterError(exception)
            );
        }
        catch (Exception exception)
        {
            return FailInvocation(definition.Descriptor.BrainId, mobileId, hookName, exception.Message);
        }
    }

    public void Reset(Serial mobileId)
    {
        if (_bindings.TryGetValue(mobileId, out var binding))
        {
            binding.State = new(_script);
        }
    }

    public bool TryBind(Serial mobileId, string brainId, out BrainDescriptor? descriptor, out string? error)
    {
        if (!_definitions.TryGetValue(brainId, out var definition))
        {
            if (!TryLoadDefinition(brainId, out definition, out error))
            {
                descriptor = null;
                return false;
            }

            _definitions[brainId] = definition;
        }

        descriptor = definition.Descriptor;
        error = null;

        if (!_bindings.TryGetValue(mobileId, out var binding) ||
            !string.Equals(binding.BrainId, definition.Descriptor.BrainId, StringComparison.Ordinal))
        {
            _bindings[mobileId] = new(definition.Descriptor.BrainId, new(_script));
        }

        return true;
    }

    public bool TryGetDescriptor(Serial mobileId, out BrainDescriptor? descriptor)
    {
        descriptor = null;

        if (!_bindings.TryGetValue(mobileId, out var binding) ||
            !_definitions.TryGetValue(binding.BrainId, out var definition))
        {
            return false;
        }

        descriptor = definition.Descriptor;

        return true;
    }

    public bool TryReload(string brainId, out BrainDescriptor? descriptor, out string? error)
    {
        if (!TryLoadDefinition(brainId, out var definition, out error))
        {
            descriptor = null;
            _metrics.RecordReload(false);
            return false;
        }

        _definitions[brainId] = definition;
        _eventBus.Publish(new BrainDefinitionReloadedEvent(brainId, definition.Descriptor));
        descriptor = definition.Descriptor;
        error = null;
        _metrics.RecordReload(true);

        return true;
    }

    public ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        BrainAssetSeeder.SeedMissing(_brainsDirectory);

        if (_watcherRegistration is not null)
        {
            return ValueTask.CompletedTask;
        }

        var watcher = _watcherFactory(_brainsDirectory);
        watcher.Filter = "*.lua";
        watcher.IncludeSubdirectories = false;
        watcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size;
        var watcherGeneration = Interlocked.Increment(ref _watcherGeneration);
        var registration = new WatcherRegistration(watcher, watcherGeneration, QueueReload);
        _watcherRegistration = registration;
        Volatile.Write(ref _watching, 1);
        registration.Start();

        return ValueTask.CompletedTask;
    }

    public ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        StopWatching();

        return ValueTask.CompletedTask;
    }

    public void Unbind(Serial mobileId)
        => _bindings.Remove(mobileId);

    private void StopWatching()
    {
        Volatile.Write(ref _watching, 0);
        Interlocked.Increment(ref _watcherGeneration);

        var registration = _watcherRegistration;
        _watcherRegistration = null;
        registration?.Dispose();

        foreach (var debounce in _debounces)
        {
            CancelDebounce(debounce.Value);
        }

        _debounces.Clear();
    }

    private static bool IsCallable(DynValue value)
        => value.Type is DataType.Function or DataType.ClrFunction;

    private static void CancelDebounce(CancellationTokenSource cancellation)
    {
        try
        {
            cancellation.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // A completed debounce may dispose its token while StopAsync is enumerating a stale entry.
        }
    }

    private static FileSystemWatcher CreateFileSystemWatcher(string path)
        => new(path);

    private static string GetHookName(NpcBrainHookType hook)
        => hook switch
        {
            NpcBrainHookType.Activate => "on_activate",
            NpcBrainHookType.Deactivate => "on_deactivate",
            NpcBrainHookType.SpeechHeard => "on_speech_heard",
            NpcBrainHookType.MobileEnteredRange => "on_mobile_entered_range",
            NpcBrainHookType.MobileLeftRange => "on_mobile_left_range",
            NpcBrainHookType.MobileMoved => "on_mobile_moved",
            NpcBrainHookType.Attacked => "on_attacked",
            NpcBrainHookType.Damage => "on_damage",
            NpcBrainHookType.Death => "on_death",
            NpcBrainHookType.Think => "think",
            _ => $"unknown({(int)hook})"
        };

    private static string GetInterpreterError(InterpreterException exception)
        => string.IsNullOrWhiteSpace(exception.DecoratedMessage) ? exception.Message : exception.DecoratedMessage;

    private static bool IsSupportedHook(NpcBrainHookType hook)
        => hook is
            NpcBrainHookType.Activate or
            NpcBrainHookType.Deactivate or
            NpcBrainHookType.SpeechHeard or
            NpcBrainHookType.MobileEnteredRange or
            NpcBrainHookType.MobileLeftRange or
            NpcBrainHookType.MobileMoved or
            NpcBrainHookType.Attacked or
            NpcBrainHookType.Damage or
            NpcBrainHookType.Death or
            NpcBrainHookType.Think;

    private static bool TryReadInteger(Table table, string name, out int value)
    {
        value = 0;
        var field = table.Get(name);

        if (field.Type != DataType.Number ||
            !double.IsFinite(field.Number) ||
            field.Number != Math.Truncate(field.Number) ||
            field.Number is < int.MinValue or > int.MaxValue)
        {
            return false;
        }

        value = (int)field.Number;

        return true;
    }

    private Table CreateEnvironment()
    {
        var environment = new Table(_script);
        environment.Set("brain", DynValue.NewTable(LuaBrainApi.Create(_script)));

        foreach (var name in AllowedGlobalNames)
        {
            var value = _script.Globals.Get(name);

            if (value.Type is not DataType.Nil and not DataType.Void)
            {
                environment.Set(name, value);
            }
        }

        foreach (var name in AllowedLibraryNames)
        {
            var sourceValue = _script.Globals.Get(name);

            if (sourceValue.Type != DataType.Table)
            {
                continue;
            }

            var copy = new Table(_script);

            foreach (var pair in sourceValue.Table.Pairs)
            {
                copy.Set(pair.Key, pair.Value);
            }

            environment.Set(name, DynValue.NewTable(copy));
        }

        return environment;
    }

    private NpcBrainInvocationResult FailInvocation(
        string brainId,
        Serial mobileId,
        string hookName,
        string error,
        bool budgetExceeded = false
    )
    {
        _logger.Error(
            "NPC brain hook failed for brain {BrainId}, mobile {MobileId}, hook {HookName}: {LuaError}",
            brainId,
            mobileId.Value,
            hookName,
            error
        );
        _metrics.RecordHookFailure(budgetExceeded);

        return NpcBrainInvocationResult.Failed(error, budgetExceeded);
    }

    private async Task DebounceReloadAsync(
        string path,
        CancellationTokenSource cancellation,
        FileSystemWatcher watcher,
        int watcherGeneration
    )
    {
        try
        {
            await Task
                .Delay(
                    TimeSpan.FromMilliseconds(ReloadDebounceMilliseconds),
                    _timeProvider,
                    cancellation.Token
                )
                .ConfigureAwait(false);

            if (!RemoveDebounce(path, cancellation) ||
                !IsCurrentWatcher(watcher, watcherGeneration))
            {
                return;
            }

            var brainId = Path.GetFileNameWithoutExtension(path);

            _gameLoop.Post(
                () =>
                {
                    if (IsCurrentWatcher(watcher, watcherGeneration))
                    {
                        TryReload(brainId, out _, out _);
                    }
                }
            );
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        finally
        {
            RemoveDebounce(path, cancellation);
            cancellation.Dispose();
        }
    }

    private bool IsCurrentWatcher(FileSystemWatcher watcher, int watcherGeneration)
    {
        var current = Volatile.Read(ref _watcherRegistration);

        return Volatile.Read(ref _watching) != 0 &&
               Volatile.Read(ref _watcherGeneration) == watcherGeneration &&
               current is not null &&
               current.Generation == watcherGeneration &&
               ReferenceEquals(current.Watcher, watcher);
    }

    private void QueueReload(
        FileSystemWatcher watcher,
        int watcherGeneration,
        object sender,
        string path
    )
    {
        if (!ReferenceEquals(sender, watcher) || !IsCurrentWatcher(watcher, watcherGeneration))
        {
            return;
        }

        var normalizedPath = Path.GetFullPath(path);
        var cancellation = new CancellationTokenSource();

        _debounces.AddOrUpdate(
            normalizedPath,
            cancellation,
            (_, previous) =>
            {
                CancelDebounce(previous);
                return cancellation;
            }
        );

        if (!IsCurrentWatcher(watcher, watcherGeneration))
        {
            CancelDebounce(cancellation);
            RemoveDebounce(normalizedPath, cancellation);
            cancellation.Dispose();
            return;
        }

        _ = DebounceReloadAsync(normalizedPath, cancellation, watcher, watcherGeneration);
    }

    private bool RemoveDebounce(string path, CancellationTokenSource cancellation)
        => ((ICollection<KeyValuePair<string, CancellationTokenSource>>)_debounces).Remove(
            new(path, cancellation)
        );

    private bool TryLoadDefinition(
        string brainId,
        out LuaBrainDefinition definition,
        out string? error
    )
    {
        definition = null!;

        if (string.IsNullOrWhiteSpace(brainId) || !ValidBrainIdPattern.IsMatch(brainId))
        {
            error = $"Brain ID '{brainId}' is invalid.";
            return false;
        }

        var path = Path.Combine(_brainsDirectory, brainId + ".lua");

        if (!File.Exists(path))
        {
            error = $"Brain file was not found: {path}";
            return false;
        }

        try
        {
            var directoryInfo = new DirectoryInfo(_brainsDirectory);
            var fileInfo = new FileInfo(path);

            if ((directoryInfo.Attributes & FileAttributes.ReparsePoint) != 0 ||
                (fileInfo.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                error = "Brain directories and files may not be symbolic links or reparse points.";
                return false;
            }

            if (fileInfo.Length > MaximumBrainFileBytes)
            {
                error = "Brain files may not exceed 256 KiB.";
                return false;
            }

            var environment = CreateEnvironment();
            var chunk = _script.LoadFile(path, environment, path);
            var coroutineValue = _script.CreateCoroutine(chunk);
            var coroutine = coroutineValue.Coroutine;
            coroutine.AutoYieldCounter = _advanced.InstructionBudget;
            var result = coroutine.Resume();

            if (coroutine.State == CoroutineState.ForceSuspended)
            {
                error = "Instruction budget exceeded while loading brain definition.";
                return false;
            }

            if (coroutine.State != CoroutineState.Dead)
            {
                error = "Brain definition chunks may not yield.";
                return false;
            }

            return TryValidateDefinition(brainId, result, out definition, out error);
        }
        catch (InterpreterException exception)
        {
            error = GetInterpreterError(exception);
            return false;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
    }

    private bool TryValidateDefinition(
        string brainId,
        DynValue value,
        out LuaBrainDefinition definition,
        out string? error
    )
    {
        definition = null!;

        if (value.Type != DataType.Table)
        {
            error = "Brain definition must return a table.";
            return false;
        }

        var strategy = value.Table;
        var id = strategy.Get("id");

        if (id.Type != DataType.String || !string.Equals(id.String, brainId, StringComparison.Ordinal))
        {
            error = $"Brain definition id must exactly match '{brainId}'.";
            return false;
        }

        if (!TryReadInteger(strategy, "default_tick_ms", out var defaultTickMilliseconds) ||
            defaultTickMilliseconds < _advanced.MinTickMilliseconds ||
            defaultTickMilliseconds > _advanced.MaxTickMilliseconds)
        {
            error =
                $"Brain default_tick_ms must be between {_advanced.MinTickMilliseconds} and "
                + $"{_advanced.MaxTickMilliseconds}.";
            return false;
        }

        if (!TryReadInteger(strategy, "perception_range", out var perceptionRange) ||
            perceptionRange is < 0 or > MaximumPerceptionRange)
        {
            error = $"Brain perception_range must be between 0 and {MaximumPerceptionRange}.";
            return false;
        }

        if (!TryReadInteger(strategy, "hearing_range", out var hearingRange) ||
            hearingRange is < 0 or > MaximumPerceptionRange)
        {
            error = $"Brain hearing_range must be between 0 and {MaximumPerceptionRange}.";
            return false;
        }

        if (!IsCallable(strategy.Get("think")))
        {
            error = "Brain think must be callable.";
            return false;
        }

        foreach (var hookName in OptionalHookNames)
        {
            var hook = strategy.Get(hookName);

            if (hook.Type is not DataType.Nil and not DataType.Void && !IsCallable(hook))
            {
                error = $"Brain {hookName} must be callable or nil.";
                return false;
            }
        }

        definition = new(
            new(brainId, defaultTickMilliseconds, perceptionRange, hearingRange),
            strategy
        );
        error = null;

        return true;
    }

    private sealed class BrainBinding
    {
        public string BrainId { get; }

        public Table State { get; set; }

        public BrainBinding(string brainId, Table state)
        {
            BrainId = brainId;
            State = state;
        }
    }

    private sealed class WatcherRegistration : IDisposable
    {
        private readonly FileSystemEventHandler _fileChanged;
        private readonly RenamedEventHandler _fileRenamed;

        public int Generation { get; }

        public FileSystemWatcher Watcher { get; }

        public WatcherRegistration(
            FileSystemWatcher watcher,
            int generation,
            Action<FileSystemWatcher, int, object, string> queueReload
        )
        {
            _fileChanged = (sender, eventArgs) =>
                queueReload(watcher, generation, sender, eventArgs.FullPath);
            _fileRenamed = (sender, eventArgs) =>
                queueReload(watcher, generation, sender, eventArgs.FullPath);
            Generation = generation;
            Watcher = watcher;
        }

        public void Start()
        {
            Watcher.Changed += _fileChanged;
            Watcher.Created += _fileChanged;
            Watcher.Renamed += _fileRenamed;
            Watcher.EnableRaisingEvents = true;
        }

        public void Dispose()
        {
            Watcher.EnableRaisingEvents = false;
            Watcher.Changed -= _fileChanged;
            Watcher.Created -= _fileChanged;
            Watcher.Renamed -= _fileRenamed;
            Watcher.Dispose();
        }
    }

    public void Dispose()
    {
        StopWatching();
    }
}
