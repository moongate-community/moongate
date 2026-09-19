using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using DryIoc;
using Lua;
using Lua.Standard;
using Moongate.Scripting.Data.Config;
using Moongate.Scripting.Data.Events;
using Moongate.Scripting.Data.Scripts;
using Moongate.Scripting.Interfaces;
using Moongate.Scripting.Internal;
using Moongate.Scripting.Modules;
using Moongate.Server.Core.Interfaces.Services;
using Serilog;

namespace Moongate.Scripting.Services;

/// <summary>Owns the single LuaState: opens the sandboxed libraries, binds modules, runs the prelude and init.lua, and serves the four IScriptEngine operations.</summary>
public sealed class LuaScriptEngineService : IScriptEngine, IMoongateStartupService, IDisposable
{
    public const int StartupPriority = 70;

    private const string PreludeResource = "Moongate.Scripting.Assets.prelude.lua";

    private readonly ILogger _logger = Log.ForContext<LuaScriptEngineService>();
    private readonly ScriptEngineOptions _options;
    private readonly IScriptModuleRegistry _registry;
    private readonly IResolverContext _resolver;
    private readonly ITimerService _timers;
    private readonly IEventBusService _eventBus;
    private readonly IScriptThreadGuard _guard;
    private readonly List<BoundModule> _boundModules = [];
    private LuaState? _state;
    private ScriptFileLoader? _files;
    private CoroutineScheduler? _scheduler;
    private ScriptOwnership? _ownership;
    private InstructionBudget? _budget;
    private long _callsStarted;
    private bool _disposed;

    /// <summary>Gets every module bound at startup, in binding order; used by the definitions generator.</summary>
    internal IReadOnlyList<BoundModule> BoundModules => _boundModules;

    public LuaScriptEngineService(
        ScriptEngineOptions options,
        IScriptModuleRegistry registry,
        IResolverContext resolver,
        IGameLoopService gameLoop,
        ITimerService timers,
        IEventBusService eventBus
    )
    {
        _options = options;
        _registry = registry;
        _resolver = resolver;
        _timers = timers;
        _eventBus = eventBus;
        _guard = new LoopThreadGuard(gameLoop);
    }

    /// <summary>Opens the sandboxed Lua libraries, binds every module, and runs the prelude and the bootstrap file.</summary>
    public Task StartAsync()
    {
        _options.Validate();
        Directory.CreateDirectory(_options.ScriptsDirectory);
        var state = LuaState.Create();
        state.OpenBasicLibrary();
        state.OpenStringLibrary();
        state.OpenTableLibrary();
        state.OpenMathLibrary();
        state.OpenCoroutineLibrary();
        state.OpenModuleLibrary();
        state.ModuleLoader = new ScriptDirectoryModuleLoader(_options.ScriptsDirectory);

        var files = new ScriptFileLoader(state, _options.ScriptsDirectory);
        var ownership = new ScriptOwnership();
        var budget = new InstructionBudget(state, _options.MaxInstructionsPerResume, _options.HookInterval);
        budget.Install();
        var scheduler = new CoroutineScheduler(state, _timers, budget, ownership, ReportError, () => files.CurrentFile);

        BindModules(state, scheduler, ownership);
        budget.Scoped(() =>
        {
            RunPrelude(state);

            return true;
        });

        _state = state;
        _files = files;
        _scheduler = scheduler;
        _ownership = ownership;
        _budget = budget;

        RunBootstrap();
        WriteDefinitions();
        _logger.Information("Script engine started with {ModuleCount} modules from {ScriptsDirectory}",
            _boundModules.Count, _options.ScriptsDirectory);
        // Module count includes the two built-ins, engine and timer.

        return Task.CompletedTask;
    }

    /// <summary>Disposes the engine, releasing the LuaState.</summary>
    public Task StopAsync()
    {
        // Pending Lua timers die with the wheel, which stops after this service (priority -900 stops last).
        Dispose();

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void LoadFile(string relativePath)
    {
        _guard.EnsureScriptThread(nameof(LoadFile));
        var files = Ready(_files);

        try
        {
            // A top-level chunk is one budget unit, like a resume.
            Ready(_budget).Scoped(() => files.Load(relativePath));
        }
        catch (Exception exception) when (exception is LuaRuntimeException or LuaCompileException)
        {
            var error = ScriptErrorParser.FromException(exception, ScriptFileLoader.Normalize(relativePath));
            ReportError(error);

            throw new InvalidOperationException($"{error.File}:{error.Line}: {error.Message}", exception);
        }
    }

    /// <inheritdoc />
    [SuppressMessage(
        "Naming",
        "CA1716:Identifiers should not match keywords",
        Justification = "'Call' is the domain term for invoking a Lua function; it is not a C# keyword."
    )]
    public ScriptResult Call(string functionName, params object?[] args)
    {
        _guard.EnsureScriptThread(nameof(Call));
        ArgumentException.ThrowIfNullOrWhiteSpace(functionName);
        var state = Ready(_state);
        var scheduler = Ready(_scheduler);
        _callsStarted++;

        if (!state.Environment.TryGetValue(functionName, out var value) || value.Type != LuaValueType.Function)
        {
            var error = new ScriptErrorInfo("", 0, $"'{functionName}' is not a global function", null);
            ReportError(error);

            return ScriptResult.Failed(error);
        }

        var owner = Ready(_files).CurrentFile ?? _options.BootstrapFile;

        return scheduler.Start(value.Read<LuaFunction>(), owner, args);
    }

    /// <inheritdoc />
    public void Invalidate(string relativePath)
    {
        _guard.EnsureScriptThread(nameof(Invalidate));
        var key = ScriptFileLoader.Normalize(relativePath);
        Ready(_scheduler).CancelOwned(key);
        Ready(_files).Invalidate(key);
    }

    /// <inheritdoc />
    public ScriptExecutionMetrics GetMetrics()
    {
        var scheduler = _scheduler;

        return new ScriptExecutionMetrics(
            _files?.FilesLoaded ?? 0,
            _callsStarted,
            scheduler?.Resumed ?? 0,
            scheduler?.Finished ?? 0,
            scheduler?.Errors ?? 0,
            scheduler?.BudgetAborts ?? 0,
            scheduler?.ActiveCount ?? 0
        );
    }

    private void BindModules(LuaState state, CoroutineScheduler scheduler, ScriptOwnership ownership)
    {
        var binder = new LuaModuleBinder(_guard);
        _boundModules.Clear();
        // Built-ins first: engine and timer depend on engine internals, so the host cannot register them.
        _boundModules.Add(binder.Bind(state, new EngineModule()));
        _boundModules.Add(binder.Bind(state, new TimerModule(_timers, scheduler, ownership)));

        foreach (var moduleType in _registry.ModuleTypes)
        {
            _boundModules.Add(binder.Bind(state, _resolver.Resolve(moduleType)));
        }

        foreach (var enumType in _registry.EnumTypes.Concat(binder.DiscoveredEnums).Distinct())
        {
            binder.BindEnum(state, enumType);
        }
    }

    private static void RunPrelude(LuaState state)
    {
        using var stream = typeof(LuaScriptEngineService).Assembly.GetManifestResourceStream(PreludeResource)
                           ?? throw new InvalidOperationException($"Embedded resource {PreludeResource} is missing.");
        using var reader = new StreamReader(stream);
        var closure = state.Load(reader.ReadToEnd().AsSpan(), "<prelude>", state.Environment);
        SyncValueTask.Run(state.ExecuteAsync(closure, default));
    }

    private void RunBootstrap()
    {
        var files = Ready(_files);
        var full = ScriptDirectoryModuleLoader.ResolvePath(_options.ScriptsDirectory, _options.BootstrapFile);

        if (!File.Exists(full))
        {
            _logger.Warning("No {BootstrapFile} under {ScriptsDirectory}; the engine starts empty",
                _options.BootstrapFile, _options.ScriptsDirectory);

            return;
        }

        try
        {
            Ready(_budget).Scoped(() => files.Load(_options.BootstrapFile));
        }
        catch (Exception exception) when (exception is LuaRuntimeException or LuaCompileException)
        {
            var error = ScriptErrorParser.FromException(exception, _options.BootstrapFile);
            _logger.Error("Bootstrap script failed at {File}:{Line}: {Message}", error.File, error.Line, error.Message);

            throw new InvalidOperationException(
                $"Script bootstrap failed at {error.File}:{error.Line}: {error.Message}", exception);
        }
    }

    private void WriteDefinitions()
    {
        // Filled in by Task 10, which adds the definitions generator.
    }

    private void ReportError(ScriptErrorInfo error)
    {
        _logger.Error("Script error at {File}:{Line}: {Message}", error.File, error.Line, error.Message);
        _ = _eventBus.PublishAsync(new ScriptErrorEvent(error));
    }

    private static T Ready<T>(T? component) where T : class
    {
        return component ?? throw new InvalidOperationException("The script engine has not started.");
    }

    /// <summary>Disposes the LuaState and releases every component created at startup.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _state?.Dispose();
        _state = null;
        _files = null;
        _scheduler = null;
        _ownership = null;
        _budget = null;
    }
}
