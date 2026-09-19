using System.Diagnostics.CodeAnalysis;
using DryIoc;
using Lua;
using Lua.Standard;
using Moongate.Scripting.Binding;
using Moongate.Scripting.Data.Binding;
using Moongate.Scripting.Data.Config;
using Moongate.Scripting.Data.Scripts;
using Moongate.Scripting.Interfaces;
using Moongate.Scripting.Internal;
using Moongate.Scripting.Modules;
using Moongate.Scripting.Utils;
using Moongate.Server.Core.Interfaces.Services;
using Serilog;

namespace Moongate.Scripting.Services;

/// <summary>Owns the single LuaState: opens the sandboxed libraries, binds modules, runs the prelude and init.lua, and serves the four IScriptEngine operations.</summary>
public sealed class LuaScriptEngineService : IScriptEngine, IMoongateStartupService, IDisposable
{
    /// <summary>Registration priority for the startup lifecycle: starts after the game loop (-800) and timers (-900) are running, and stops before them.</summary>
    public const int StartupPriority = 70;

    private const string PreludeResource = "Moongate.Scripting.Assets.prelude.lua";

    private readonly ILogger _logger = Log.ForContext<LuaScriptEngineService>();
    private readonly ScriptEngineOptions _options;
    private readonly IScriptModuleRegistry _registry;
    private readonly IResolverContext _resolver;
    private readonly IGameLoopService _gameLoop;
    private readonly ITimerService _timers;
    private readonly IEventBusService _eventBus;
    private readonly IScriptThreadGuard _guard;
    private readonly List<BoundModule> _boundModules = [];
    private List<Type> _publishedEnums = [];
    private LuaState? _state;
    private ScriptFileLoader? _files;
    private CoroutineScheduler? _scheduler;
    private InstructionBudget? _budget;
    private long _callsStarted;
    private bool _disposed;

    /// <summary>Gets every module bound at startup, in binding order; used by the definitions generator.</summary>
    internal IReadOnlyList<BoundModule> BoundModules => _boundModules;

    /// <summary>Initializes a new instance of the <see cref="LuaScriptEngineService"/> class. Construction does no Lua work; <see cref="StartAsync"/> builds the state.</summary>
    /// <param name="options">Scripts directory, budget and bootstrap settings.</param>
    /// <param name="registry">Module and enum types to bind at startup, gathered from the container.</param>
    /// <param name="resolver">Resolves each registered module type to an instance.</param>
    /// <param name="gameLoop">Checked before every loop-affine member; error events are posted back to it.</param>
    /// <param name="timers">Wheel that fires script timers and coroutine resumes.</param>
    /// <param name="eventBus">Publishes <see cref="Moongate.Scripting.Data.Events.ScriptErrorEvent"/> for every script failure.</param>
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
        _gameLoop = gameLoop;
        _timers = timers;
        _eventBus = eventBus;
        _guard = new LoopThreadGuard(gameLoop);
    }

    /// <summary>Opens the sandboxed Lua libraries, binds every module, and runs the prelude and the bootstrap file.</summary>
    public async Task StartAsync()
    {
        if (_disposed)
        {
            throw new InvalidOperationException("The script engine has been disposed and cannot be restarted.");
        }

        if (_state is not null)
        {
            throw new InvalidOperationException("The script engine has already started.");
        }

        await RunOnLoopAsync(Start).ConfigureAwait(false);
    }

    /// <summary>Disposes the engine, releasing the LuaState.</summary>
    public async Task StopAsync()
    {
        await RunOnLoopAsync(Dispose).ConfigureAwait(false);
    }

    private void Start()
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
        WriteDefinitions();
        budget.Scoped(() =>
        {
            RunPrelude(state);

            return true;
        });

        _state = state;
        _files = files;
        _scheduler = scheduler;
        _budget = budget;

        RunBootstrap();
        _logger.Information("Script engine started with {ModuleCount} modules from {ScriptsDirectory}",
            _boundModules.Count, _options.ScriptsDirectory);
        // Module count includes the two built-ins, engine and timer.
    }

    /// <summary>
    /// Runs a lifecycle step on the loop thread. Called from the bootstrap thread in production, so the step is posted and
    /// awaited; unit tests and any caller already on the loop run it inline, and a loop that no longer accepts work
    /// (it faulted, or stopped before this service) runs it inline as well so shutdown still completes.
    /// </summary>
    private async Task RunOnLoopAsync(Action step)
    {
        if (_gameLoop.IsOnLoopThread)
        {
            step();

            return;
        }

        var item = new ScriptLifecycleWorkItem(step);

        try
        {
            await _gameLoop.PostAsync(item).ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            step();

            return;
        }

        await item.Completion.ConfigureAwait(false);
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

    /// <summary>Returns a snapshot of the execution counters. Unlike the other members this may be called from any thread; diagnostics collectors run off the loop.</summary>
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

        _publishedEnums = binder.PublishedEnums.ToList();
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
            ReportError(error);

            throw new InvalidOperationException(
                $"Script bootstrap failed at {error.File}:{error.Line}: {error.Message}", exception);
        }
    }

    private void WriteDefinitions()
    {
        if (!_options.WriteDefinitions)
        {
            return;
        }

        LuaDefinitionsGenerator.Write(_options.ScriptsDirectory, _boundModules, _publishedEnums);
    }

    private void ReportError(ScriptErrorInfo error)
    {
        _logger.Error("Script error at {File}:{Line}: {Message}", error.File, error.Line, error.Message);

        // Posted rather than published in place: onError can run inside a coroutine resume, and a bus
        // handler that calls back into the engine would be refused (and silently swallowed by the bus)
        // while that resume is still on the stack. Posting moves the handler to the next loop drain.
        if (!_gameLoop.TryPost(new ScriptErrorPublishWorkItem(_eventBus, error)))
        {
            _logger.Warning("Script error event for {File}:{Line} could not be queued", error.File, error.Line);
        }
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
        // Cancel every timer and coroutine the scheduler knows about before the state goes away: a
        // periodic timer.every callback that fires after this point must not reach CreateCoroutine on
        // a disposed state (the timer wheel and the loop stop after this service does).
        _scheduler?.CancelAll();
        _state?.Dispose();
        _state = null;
        _files = null;
        _scheduler = null;
        _budget = null;
    }
}
