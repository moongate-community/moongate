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
/// <remarks>
/// <para>
/// The sandbox opens the base, <c>string</c>, <c>table</c>, <c>math</c>, <c>coroutine</c> and
/// <c>package</c> libraries; <c>io</c>, <c>os</c> and <c>debug</c> are never opened. It then removes
/// <c>dofile</c>, <c>loadfile</c> and <c>rawset</c>; <c>package.searchpath</c>, <c>package.path</c>,
/// <c>package.cpath</c>, <c>package.loadlib</c> and the runtime's second <c>package.searchers</c> entry,
/// which resolves <c>package.path</c> on the host filesystem independently of the module loader; and
/// <c>coroutine.create</c>, <c>coroutine.wrap</c> and <c>coroutine.resume</c>, whose threads would carry
/// neither the instruction budget's hook nor its cancellation token. <c>coroutine.yield</c>,
/// <c>coroutine.status</c> and <c>coroutine.running</c> stay, so the prelude's <c>wait</c> keeps working.
/// <c>print</c> is replaced by a function that writes to the server log at Information level, under the
/// script that called it, so script output never bypasses the configured sinks.
/// </para>
/// <para>
/// Memory is not bounded: the budget counts instructions, and a single <c>string.rep</c> or table
/// constructor can allocate freely. A cap is a follow-up.
/// </para>
/// </remarks>
public sealed class LuaScriptEngineService : IScriptEngine, IMoongateStartupService, IDisposable
{
    /// <summary>Registration priority for the startup lifecycle: starts after the game loop (-800) and timers (-900) are running, and stops before them.</summary>
    public const int StartupPriority = 70;

    private const string PreludeResource = "Moongate.Scripting.Assets.prelude.lua";

    private readonly ILogger _logger = Log.ForContext<LuaScriptEngineService>();
    private readonly ILogger _scriptOutput = Log.ForContext<LogModule>();
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
    private long _chunkBudgetAborts;
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

        await RunOnLoopAsync(Start, "start").ConfigureAwait(false);
    }

    /// <summary>Disposes the engine, releasing the LuaState.</summary>
    public async Task StopAsync()
    {
        await RunOnLoopAsync(Dispose, "stop").ConfigureAwait(false);
    }

    private void Start()
    {
        _options.Validate();
        Directory.CreateDirectory(_options.ScriptsDirectory);
        var state = LuaState.Create();
        ScriptFileLoader files;
        CoroutineScheduler scheduler;
        InstructionBudget budget;

        try
        {
            state.OpenBasicLibrary();
            state.OpenStringLibrary();
            state.OpenTableLibrary();
            state.OpenMathLibrary();
            state.OpenCoroutineLibrary();
            state.OpenModuleLibrary();
            TrimSandbox(state);
            state.ModuleLoader = new ScriptDirectoryModuleLoader(_options.ScriptsDirectory);

            files = new ScriptFileLoader(state, _options.ScriptsDirectory);
            var ownership = new ScriptOwnership();
            budget = new InstructionBudget(
                state,
                _options.MaxInstructionsPerResume,
                _options.MaxInstructionsPerChunk,
                _options.HookInterval
            );
            budget.Install();
            scheduler = new CoroutineScheduler(state, _timers, budget, ownership, ReportError, () => files.CurrentFile);

            BindModules(state, scheduler, ownership);
            state.Environment["print"] = new LuaValue(CreatePrint(scheduler));
            WriteDefinitions();
            budget.Chunk(token =>
            {
                RunPrelude(state, token);

                return true;
            });
        }
        catch
        {
            // Nothing else holds the state until _state is assigned, so a binding or prelude failure
            // would leak it; Dispose only releases what the engine already owns.
            state.Dispose();

            throw;
        }

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
    /// (it faulted, or stopped before this service) runs it inline as well so shutdown still completes. A loop that
    /// accepts the step and then stops before running it fails the step rather than leaving it awaited forever.
    /// </summary>
    /// <param name="step">The lifecycle step to run.</param>
    /// <param name="stepName">Name of the step for the log line and the failure message, such as "start".</param>
    private async Task RunOnLoopAsync(Action step, string stepName)
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
            _logger.Warning("The game loop is not accepting work; running {Step} inline", stepName);
            step();

            return;
        }

        // The item was admitted, but a loop that faults or stops before draining it would never complete
        // it; waiting on the loop as well keeps the lifecycle from hanging.
        await Task.WhenAny(item.Completion, _gameLoop.Completion).ConfigureAwait(false);

        if (!item.Completion.IsCompleted)
        {
            throw new InvalidOperationException($"The game loop stopped before the script engine's {stepName} could run.");
        }

        await item.Completion.ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void LoadFile(string relativePath)
    {
        _guard.EnsureScriptThread(nameof(LoadFile));
        var files = Ready(_files);
        var budget = Ready(_budget);
        var file = ScriptFileLoader.Normalize(relativePath);

        try
        {
            // A top-level chunk is one budget unit, with the larger chunk limit.
            budget.Chunk(token => files.Load(file, token));
        }
        // A missing file is the caller's error and stays as it is; a cancellation is not this engine's
        // to report. Everything else becomes a script error: a bad chunk must not fault the game loop,
        // and neither must a failure to read it or a failure inside the runtime itself.
        catch (Exception exception) when (exception is not (FileNotFoundException or OperationCanceledException))
        {
            if (exception is ScriptBudgetExceededException)
            {
                _chunkBudgetAborts++;
            }

            var error = exception is LuaRuntimeException or LuaCompileException
                ? ScriptErrorParser.FromException(exception, file)
                : new ScriptErrorInfo(file, 0, exception.Message, null);
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
        var owner = Ready(_files).CurrentFile ?? _options.BootstrapFile;
        _callsStarted++;

        if (!state.Environment.TryGetValue(functionName, out var value) || value.Type != LuaValueType.Function)
        {
            var error = new ScriptErrorInfo(owner, 0, $"'{functionName}' is not a global function", null);
            ReportError(error);

            return ScriptResult.Failed(error);
        }

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
            (scheduler?.BudgetAborts ?? 0) + _chunkBudgetAborts,
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

    /// <summary>
    /// Builds the host half of the replacement for the base library's <c>print</c>: it takes one line and
    /// writes it to the log at Information level under the file that owns the running code. The prelude
    /// wraps it so every argument goes through Lua's own <c>tostring</c> (honouring <c>__tostring</c>) and
    /// the results are joined with tabs, exactly as the standard <c>print</c> does.
    /// </summary>
    private LuaFunction CreatePrint(IScriptScheduler scheduler)
    {
        return new LuaFunction("print", (context, _) =>
        {
            _guard.EnsureScriptThread("print");
            var line = context.ArgumentCount == 0 ? "" : context.GetArgument<string>(0);
            _scriptOutput.Information("{ScriptFile}: {Output}", scheduler.CurrentOwner ?? _options.BootstrapFile, line);

            return new ValueTask<int>(context.Return());
        });
    }

    /// <summary>
    /// Removes the standard-library members that reach past the scripts directory or past the instruction
    /// budget. Runs once, after the libraries are opened and before any script does.
    /// </summary>
    private static void TrimSandbox(LuaState state)
    {
        // dofile and loadfile read any path on the host, and loadfile returns the parser error, which
        // leaks file contents. rawset writes straight through a module's read-only proxy.
        state.Environment["dofile"] = LuaValue.Nil;
        state.Environment["loadfile"] = LuaValue.Nil;
        state.Environment["rawset"] = LuaValue.Nil;

        if (state.Environment["package"].TryRead<LuaTable>(out var package))
        {
            // The runtime installs two require searchers: the first asks state.ModuleLoader, the second
            // resolves package.path on the host filesystem independently of it, and package.path is
            // writable from Lua. Dropping the second searcher and the members that feed it confines
            // require to the scripts directory; an unknown module then reports "not found".
            if (package["searchers"].TryRead<LuaTable>(out var searchers))
            {
                searchers[2] = LuaValue.Nil;
            }

            package["searchpath"] = LuaValue.Nil;
            package["path"] = LuaValue.Nil;
            package["cpath"] = LuaValue.Nil;
            package["loadlib"] = LuaValue.Nil;
        }

        if (state.Environment["coroutine"].TryRead<LuaTable>(out var coroutine))
        {
            // A coroutine a script creates carries neither the budget hook nor the unit's cancellation
            // token, so it would run unbounded. The scheduler is the only thing that creates coroutines;
            // scripts reach the scheduler through wait(), which yields.
            coroutine["create"] = LuaValue.Nil;
            coroutine["wrap"] = LuaValue.Nil;
            coroutine["resume"] = LuaValue.Nil;
        }
    }

    private static void RunPrelude(LuaState state, CancellationToken cancellationToken)
    {
        using var stream = typeof(LuaScriptEngineService).Assembly.GetManifestResourceStream(PreludeResource)
                           ?? throw new InvalidOperationException($"Embedded resource {PreludeResource} is missing.");
        using var reader = new StreamReader(stream);
        var closure = state.Load(reader.ReadToEnd().AsSpan(), "<prelude>", state.Environment);
        SyncValueTask.Run(state.ExecuteAsync(closure, cancellationToken));
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
            Ready(_budget).Chunk(token => files.Load(_options.BootstrapFile, token));
        }
        catch (Exception exception) when (exception is LuaRuntimeException or LuaCompileException)
        {
            if (exception is ScriptBudgetExceededException)
            {
                _chunkBudgetAborts++;
            }

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

    private T Ready<T>(T? component) where T : class
    {
        return component ?? throw new InvalidOperationException(
            _disposed ? "The script engine has been disposed." : "The script engine has not started.");
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
