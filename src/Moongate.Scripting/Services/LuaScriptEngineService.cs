using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;
using DryIoc;
using Moongate.Core.Data.Directories;
using Moongate.Core.Extensions.Strings;
using Moongate.Core.Json;
using Moongate.Scripting.Attributes.Scripts;
using Moongate.Scripting.Context;
using Moongate.Scripting.Data.Config;
using Moongate.Scripting.Data.Internal;
using Moongate.Scripting.Data.Internal.Reputation;
using Moongate.Scripting.Data.Luarc;
using Moongate.Scripting.Data.Scripts;
using Moongate.Scripting.Descriptors;
using Moongate.Scripting.Interfaces;
using Moongate.Scripting.Internal;
using Moongate.Scripting.Loaders;
using Moongate.Scripting.Utils;
using Moongate.UO.Data.Utils;
using MoonSharp.Interpreter;
using Serilog;

#pragma warning disable IL2026 // RequiresUnreferencedCode - Lua scripting uses reflection for dynamic functionality
#pragma warning disable IL2072 // DynamicallyAccessedMemberTypes - Reflection access is necessary for scripting

namespace Moongate.Scripting.Services;

/// <summary>
/// Lua engine service that integrates MoonSharp with the SquidCraft game engine
/// Provides script execution, module loading, and Lua meta file generation
/// </summary>
public class LuaScriptEngineService : IScriptEngineService, IDisposable
{
    private static readonly string[] collection = ["log", "delay"];

    private readonly LuaEngineConfig _engineConfig;

    private const string OnReadyFunctionName = "on_ready";

    private const string OnEngineRunFunctionName = "on_initialize";
    private const string ReputationTitlesGlobalName = "reputation_titles_config";

    // Thread-safe collections
    private readonly ConcurrentDictionary<string, Action<object[]>> _callbacks = new();
    private readonly ConcurrentDictionary<string, object> _constants = new();
    private readonly ConcurrentDictionary<string, LuaPluginManifest> _loadedPlugins = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _manualModuleFunctions = new();

    private readonly DirectoriesConfig _directoriesConfig;
    private readonly List<string> _initScripts;
    private readonly ConcurrentDictionary<string, object> _loadedModules = new();
    private readonly ILogger _logger = Log.ForContext<LuaScriptEngineService>();

    private readonly LuaModuleLoader _moduleLoader;
    private readonly LuaScriptCache _scriptCache;
    private readonly Lock _scriptExecutionSync = new();
    private readonly List<ScriptModuleData> _scriptModules;
    private readonly List<ScriptUserData> _loadedUserData;

    private readonly IContainer _serviceProvider;

    private bool _disposed;
    private bool _isInitialized;
    private Func<string, string> _nameResolver;

    /// <summary>
    /// Initializes a new instance of the LuaScriptEngineService class.
    /// </summary>
    /// <param name="directoriesConfig">The directories configuration.</param>
    /// <param name="scriptModules">The list of script modules.</param>
    /// <param name="loadedUserData">The list of loaded user data.</param>
    /// <param name="serviceProvider">The service provider.</param>
    /// <param name="versionService">The version service.</param>
    public LuaScriptEngineService(
        DirectoriesConfig directoriesConfig,
        List<ScriptModuleData> scriptModules,
        IContainer serviceProvider,
        LuaEngineConfig engineConfig,
        List<ScriptUserData> loadedUserData = null
    )
    {
        JsonUtils.RegisterJsonContext(MoongateLuaScriptJsonContext.Default);

        ArgumentNullException.ThrowIfNull(directoriesConfig);
        ArgumentNullException.ThrowIfNull(scriptModules);
        ArgumentNullException.ThrowIfNull(serviceProvider);

        _scriptModules = scriptModules;
        _directoriesConfig = directoriesConfig;
        _serviceProvider = serviceProvider;
        _engineConfig = engineConfig;
        _loadedUserData = loadedUserData ?? new();
        _initScripts = ["bootstrap.lua", "init.lua", "main.lua"];

        CreateNameResolver();

        LuaScript = CreateOptimizedEngine();
        _scriptCache = new();
        _moduleLoader = new(LuaScript, _serviceProvider, name => _nameResolver(name), _logger);

        LoadToUserData();
    }

    /// <summary>
    /// Gets the MoonSharp script instance.
    /// </summary>
    public Script LuaScript { get; }

    /// <summary>
    /// Event raised when a script error occurs
    /// </summary>
    public event EventHandler<ScriptErrorInfo>? OnScriptError;

    /// <summary>
    /// Gets the script engine instance.
    /// </summary>
    public object Engine => LuaScript;

    /// <summary>
    /// Adds a callback function that can be called from Lua scripts.
    /// </summary>
    /// <param name="name">The name of the callback.</param>
    /// <param name="callback">The callback action.</param>
    public void AddCallback(string name, Action<object[]> callback)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(callback);

        var normalizedName = name.ToSnakeCaseUpper();
        _callbacks[normalizedName] = callback;

        _logger.Debug("Callback registered: {Name}", normalizedName);
    }

    /// <summary>
    /// Adds a constant value that can be accessed from Lua scripts.
    /// </summary>
    /// <param name="name">The name of the constant.</param>
    /// <param name="value">The value of the constant.</param>
    public void AddConstant(string name, object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var normalizedName = name.ToSnakeCaseUpper();

        if (_constants.ContainsKey(normalizedName))
        {
            _logger.Warning("Constant {Name} already exists, overwriting", normalizedName);
        }

        _constants[normalizedName] = value;

        var valueToSet = value;

        if (value != null && !IsSimpleType(value.GetType()))
        {
            valueToSet = LuaReflectionHelper.ObjectToTable(LuaScript, value);
        }

        LuaScript.Globals[normalizedName] = valueToSet;

        _logger.Debug("Constant added: {Name}", normalizedName);
    }

    /// <summary>
    /// Adds an initialization script.
    /// </summary>
    /// <param name="script">The script to add.</param>
    public void AddInitScript(string script)
    {
        if (string.IsNullOrWhiteSpace(script))
        {
            throw new ArgumentException("Script cannot be null or empty", nameof(script));
        }

        _initScripts.Add(script);
    }

    /// <summary>
    /// Adds a manual module function that can be called from Lua scripts with a callback.
    /// </summary>
    public void AddManualModuleFunction(string moduleName, string functionName, Action<object[]> callback)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);
        ArgumentException.ThrowIfNullOrWhiteSpace(functionName);
        ArgumentNullException.ThrowIfNull(callback);

        var (normalizedModule, normalizedFunction, moduleTable) = PrepareManualModule(moduleName, functionName);

        moduleTable[normalizedFunction] = DynValue.NewCallback(
            (_, args) =>
            {
                try
                {
                    var parameters = LuaReflectionHelper.ConvertArgumentsToArray(args);
                    callback(parameters);

                    return DynValue.Nil;
                }
                catch (Exception ex)
                {
                    _logger.Error(
                        ex,
                        "Error executing manual module action {FunctionName} in {ModuleName}",
                        normalizedFunction,
                        normalizedModule
                    );

                    throw new ScriptRuntimeException(ex.Message);
                }
            }
        );

        RegisterManualModuleFunction(normalizedModule, normalizedFunction);
    }

    /// <summary>
    /// Adds a manual module function with typed input and output that can be called from Lua scripts.
    /// </summary>
    public void AddManualModuleFunction<TInput, TOutput>(
        string moduleName,
        string functionName,
        Func<TInput?, TOutput> callback
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);
        ArgumentException.ThrowIfNullOrWhiteSpace(functionName);
        ArgumentNullException.ThrowIfNull(callback);

        var (normalizedModule, normalizedFunction, moduleTable) = PrepareManualModule(moduleName, functionName);

        moduleTable[normalizedFunction] = DynValue.NewCallback(
            (_, args) =>
            {
                try
                {
                    var input = LuaReflectionHelper.PrepareManualInput<TInput>(args);
                    var result = callback(input);

                    return LuaReflectionHelper.ConvertToLua(LuaScript, result);
                }
                catch (Exception ex)
                {
                    _logger.Error(
                        ex,
                        "Error executing manual module function {FunctionName} in {ModuleName}",
                        normalizedFunction,
                        normalizedModule
                    );

                    throw new ScriptRuntimeException(ex.Message);
                }
            }
        );

        RegisterManualModuleFunction(normalizedModule, normalizedFunction);
    }

    /// <summary>
    /// Adds a script module to the engine.
    /// </summary>
    /// <param name="type">The type of the script module.</param>
    public void AddScriptModule(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        _scriptModules.Add(new(type));
    }

    public void CallFunction(string functionName, params object[] args)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(functionName);

        var luaFunction = LuaScript.Globals.Get(functionName);

        if (luaFunction.Type == DataType.Function)
        {
            try
            {
                var dynArgs = new DynValue[args.Length];

                for (var i = 0; i < args.Length; i++)
                {
                    dynArgs[i] = LuaReflectionHelper.ConvertToLua(LuaScript, args[i]);
                }

                LuaScript.Call(luaFunction, dynArgs);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error calling Lua function {FunctionName}", functionName);

                throw;
            }
        }
        else
        {
            _logger.Verbose("Lua function {FunctionName} not found or is not a function", functionName);
        }
    }

    /// <summary>
    /// Clears the script cache
    /// </summary>
    public void ClearScriptCache()
    {
        _scriptCache.Clear();
        _logger.Information("Script cache cleared");
    }

    /// <summary>
    /// Invalidates one cached script file.
    /// </summary>
    /// <param name="filePath">The script file path.</param>
    public void InvalidateScript(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (_scriptCache.Invalidate(filePath))
        {
            _logger.Debug("Script cache invalidated: {FilePath}", filePath);

            return;
        }

        _logger.Debug("No cached script entry found to invalidate: {FilePath}", filePath);
    }

    /// <summary>
    /// Disposes of the resources used by the LuaScriptEngineService.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            _loadedModules.Clear();
            _callbacks.Clear();
            _constants.Clear();

            GC.SuppressFinalize(this);

            _logger.Debug("Lua engine disposed successfully");
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Error during Lua engine disposal");
        }
        finally
        {
            _disposed = true;
        }
    }

    /// <summary>
    /// Executes a registered callback with the specified arguments.
    /// </summary>
    /// <param name="name">The name of the callback.</param>
    /// <param name="args">The arguments to pass to the callback.</param>
    public void ExecuteCallback(string name, params object[] args)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var normalizedName = name.ToSnakeCaseUpper();

        if (_callbacks.TryGetValue(normalizedName, out var callback))
        {
            try
            {
                _logger.Debug("Executing callback {Name}", normalizedName);
                callback(args);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error executing callback {Name}", normalizedName);

                throw;
            }
        }
        else
        {
            _logger.Warning("Callback {Name} not found", normalizedName);
        }
    }

    /// <summary>
    /// Executes the engine ready function from bootstrap scripts.
    /// </summary>
    public void ExecuteEngineReady()
        => ExecuteFunctionFromBootstrap(OnEngineRunFunctionName);

    /// <summary>
    /// Executes a Lua function and returns the result.
    /// </summary>
    /// <param name="command">The function command to execute.</param>
    /// <returns>The result of the function execution.</returns>
    public ScriptResult ExecuteFunction(string command)
    {
        try
        {
            var result = LuaScript.DoString($"return {command}");

            return ScriptResultBuilder.CreateSuccess().WithData(result.ToObject()).Build();
        }
        catch (ScriptRuntimeException luaEx)
        {
            var errorInfo = LuaReflectionHelper.CreateErrorInfo(luaEx, command);
            OnScriptError?.Invoke(this, errorInfo);

            _logger.Error(
                luaEx,
                "Lua error at line {Line}, column {Column}: {Message}",
                errorInfo.LineNumber,
                errorInfo.ColumnNumber,
                errorInfo.Message
            );

            return ScriptResultBuilder.CreateError()
                                      .WithMessage(
                                          $"{errorInfo.ErrorType}: {errorInfo.Message} at line {errorInfo.LineNumber}"
                                      )
                                      .Build();
        }
        catch (InterpreterException luaEx)
        {
            var errorInfo = LuaReflectionHelper.CreateErrorInfo(luaEx, command);
            OnScriptError?.Invoke(this, errorInfo);

            _logger.Error(
                luaEx,
                "Lua {ErrorType} at line {Line}, column {Column}: {Message}",
                errorInfo.ErrorType,
                errorInfo.LineNumber,
                errorInfo.ColumnNumber,
                errorInfo.Message
            );

            return ScriptResultBuilder.CreateError()
                                      .WithMessage(
                                          $"{errorInfo.ErrorType}: {errorInfo.Message} at line {errorInfo.LineNumber}"
                                      )
                                      .Build();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to execute function: {Command}", command);

            return ScriptResultBuilder.CreateError().WithMessage(ex.Message).Build();
        }
    }

    /// <summary>
    /// Executes a Lua function asynchronously and returns the result.
    /// </summary>
    /// <param name="command">The function command to execute.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task<ScriptResult> ExecuteFunctionAsync(string command)
        => ExecuteFunction(command);

    public void ExecuteFunctionFromBootstrap(string name)
    {
        try
        {
            var onReadyFunc = LuaScript.Globals.Get(name);

            if (onReadyFunc.Type == DataType.Nil)
            {
                _logger.Warning("No {FuncName} function defined in scripts", name);

                return;
            }

            // Verify it's actually a function before calling
            if (onReadyFunc.Type != DataType.Function)
            {
                _logger.Error(
                    "'{FuncName}' is defined but is not a function, it's a {Type}. Skipping execution.",
                    name,
                    onReadyFunc.Type
                );

                return;
            }

            LuaScript.Call(onReadyFunc);
            _logger.Debug("Boot function executed successfully");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error executing onReady function");

            throw;
        }
    }

    /// <summary>
    /// Executes a script string.
    /// </summary>
    /// <param name="script">The script to execute.</param>
    public void ExecuteScript(string script)
        => ExecuteScript(script, null);

    /// <summary>
    /// Executes a script from a file.
    /// </summary>
    /// <param name="scriptFile">The path to the script file.</param>
    public void ExecuteScriptFile(string scriptFile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scriptFile);

        if (!File.Exists(scriptFile))
        {
            throw new FileNotFoundException($"Script file not found: {scriptFile}", scriptFile);
        }

        try
        {
            var content = File.ReadAllText(scriptFile);
            _logger.Debug("Executing script file: {FileName}", Path.GetFileName(scriptFile));
            ExecuteScript(content, scriptFile);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to execute script file: {FileName}", Path.GetFileName(scriptFile));
        }
    }

    /// <summary>
    /// Executes a script file asynchronously.
    /// </summary>
    /// <param name="scriptFile">The path to the script file.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task ExecuteScriptFileAsync(string scriptFile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scriptFile);

        if (!File.Exists(scriptFile))
        {
            throw new FileNotFoundException($"Script file not found: {scriptFile}", scriptFile);
        }

        try
        {
            var content = await File.ReadAllTextAsync(scriptFile).ConfigureAwait(false);
            _logger.Debug("Executing script file asynchronously: {FileName}", Path.GetFileName(scriptFile));
            ExecuteScript(content, scriptFile);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to execute script file asynchronously: {FileName}", Path.GetFileName(scriptFile));

            throw;
        }
    }

    /// <summary>
    /// Gets execution metrics for performance monitoring
    /// </summary>
    public ScriptExecutionMetrics GetExecutionMetrics()
        => _scriptCache.GetMetrics();

    /// <summary>
    /// Gets the statistics of the script engine.
    /// </summary>
    /// <returns>A tuple containing the module count, callback count, constant count, and initialization status.</returns>
    public (int ModuleCount, int CallbackCount, int ConstantCount, bool IsInitialized) GetStats()
        => (_loadedModules.Count, _callbacks.Count, _constants.Count, _isInitialized);

    /// <summary>
    /// Registers a global variable in the Lua environment.
    /// </summary>
    /// <param name="name">The name of the variable.</param>
    /// <param name="value">The value of the variable.</param>
    public void RegisterGlobal(string name, object value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(value);

        LuaScript.Globals[name] = value;
        _logger.Debug("Global registered: {Name} (Type: {Type})", name, value.GetType().Name);
    }

    /// <summary>
    /// Registers a global function in the Lua environment.
    /// </summary>
    /// <param name="name">The name of the function.</param>
    /// <param name="func">The delegate representing the function.</param>
    public void RegisterGlobalFunction(string name, Delegate func)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(func);

        LuaScript.Globals[name] = func;
        _logger.Debug("Global function registered: {Name}", name);
    }

    /// <summary>
    /// Registers a global type user data.
    /// </summary>
    /// <param name="type">The type to register.</param>
    public void RegisterGlobalTypeUserData(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        _logger.Debug("Global type user data registered: {TypeName}", type.Name);

        LuaScript.Globals[type.Name] = UserData.CreateStatic(type);
    }

    /// <summary>
    /// Registers a global type user data for the specified type.
    /// </summary>
    /// <typeparam name="T">The type to register.</typeparam>
    public void RegisterGlobalTypeUserData<T>()
    {
        var type = typeof(T);
        _logger.Debug("Global type user data registered: {TypeName}", type.Name);

        LuaScript.Globals[type.Name] = UserData.CreateStatic(type);
    }

    /// <summary>
    /// Resets the script engine to its initial state.
    /// </summary>
    public void Reset()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _loadedModules.Clear();
        _loadedPlugins.Clear();
        _callbacks.Clear();
        _constants.Clear();
        _isInitialized = false;

        _logger.Debug("Lua engine reset");
    }

    /// <summary>
    /// Stops the script engine asynchronously.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task ShutdownAsync()
        => Task.CompletedTask;

    /// <summary>
    /// Starts the script engine asynchronously.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task StartAsync()
    {
        if (_isInitialized)
        {
            _logger.Warning("Script engine is already initialized");

            return;
        }

        try
        {
            _moduleLoader.RegisterScriptModules(_scriptModules, _loadedModules, CancellationToken.None);
            _moduleLoader.RegisterEnums(LuaDocumentationGenerator.FoundEnums);

            AddConstant("version", _engineConfig.EngineVersion);
            AddConstant("engine", "Moongate");
            AddConstant("platform", Environment.OSVersion.Platform.ToString());

            await GenerateLuaMetaFileAsync(CancellationToken.None);

            _moduleLoader.RegisterGlobalFunctions();

            ExecuteBootstrap();
            InitializeReputationTitles();

            ExecuteBootFunction();
            _isInitialized = true;
            _logger.Information("Lua engine initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to initialize Lua engine");

            throw;
        }
    }

    public Task StopAsync()
    {
        _logger.Information("Lua engine stopped");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Converts a name to the script engine function name format.
    /// </summary>
    /// <param name="name">The name to convert.</param>
    /// <returns>The converted function name.</returns>
    public string ToScriptEngineFunctionName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return _nameResolver(name);
    }

    /// <summary>
    /// Unregisters a global variable from the Lua environment.
    /// </summary>
    /// <param name="name">The name of the variable to unregister.</param>
    /// <returns>True if the variable was unregistered, false otherwise.</returns>
    public bool UnregisterGlobal(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var existingValue = LuaScript.Globals.Get(name);

        if (existingValue.Type != DataType.Nil)
        {
            LuaScript.Globals[name] = DynValue.Nil;
            _logger.Debug("Global unregistered: {Name}", name);

            return true;
        }

        _logger.Warning("Attempted to unregister non-existent global: {Name}", name);

        return false;
    }

    private void CreateNameResolver()
        => _nameResolver = name => name.ToSnakeCase();

    // _nameResolver = _scriptEngineConfig.ScriptNameConversion switch
    // {
    //     ScriptNameConversion.CamelCase  => name => name.ToCamelCase(),
    //     ScriptNameConversion.PascalCase => name => name.ToPascalCase(),
    //     ScriptNameConversion.SnakeCase  => name => name.ToSnakeCase(),
    //     _                               => _nameResolver
    // };
    private Script CreateOptimizedEngine()
    {
        var script = new Script
        {
            Options =
            {
                // Configure MoonSharp options
                DebugPrint = s => _logger.Debug("[Lua] {Message}", s),
                ScriptLoader = new LuaScriptLoader(_engineConfig.ScriptsDirectory, _engineConfig.PluginsDirectory)
            }
        };

        _logger.Debug("Lua script loader configured for require() functionality");

        return script;
    }

    private void ExecuteBootFunction()
        => ExecuteFunctionFromBootstrap(OnReadyFunctionName);

    private void ExecuteBootstrap()
    {
        foreach (var file in _initScripts.Select(s => Path.Combine(_engineConfig.ScriptsDirectory, s)))
        {
            if (File.Exists(file))
            {
                var fileName = Path.GetFileName(file);
                _logger.Information("Executing {FileName} script", fileName);
                ExecuteScriptFile(file);
            }
        }

        ExecutePluginsBootstrap();
    }

    private void ExecutePluginsBootstrap()
    {
        var pluginsDirectory = _engineConfig.PluginsDirectory;

        _loadedPlugins.Clear();

        if (string.IsNullOrWhiteSpace(pluginsDirectory) || !Directory.Exists(pluginsDirectory))
        {
            return;
        }

        foreach (var pluginPath in Directory.EnumerateDirectories(pluginsDirectory)
                                            .OrderBy(path => path, StringComparer.Ordinal))
        {
            var manifestPath = Path.Combine(pluginPath, "plugin.lua");

            if (!File.Exists(manifestPath))
            {
                continue;
            }

            try
            {
                var manifest = LoadPluginManifest(manifestPath);

                if (manifest is null)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(manifest.Id) || string.IsNullOrWhiteSpace(manifest.Entry))
                {
                    _logger.Warning("Skipping Lua plugin at {PluginPath}: missing id or entry", pluginPath);

                    continue;
                }

                if (!_loadedPlugins.TryAdd(manifest.Id, manifest))
                {
                    _logger.Warning(
                        "Skipping Lua plugin at {PluginPath}: duplicate plugin id {PluginId}",
                        pluginPath,
                        manifest.Id
                    );

                    continue;
                }

                var entryPath = Path.Combine(pluginPath, manifest.Entry);

                if (!File.Exists(entryPath))
                {
                    _loadedPlugins.TryRemove(manifest.Id, out _);
                    _logger.Warning(
                        "Skipping Lua plugin {PluginId}: entry script not found at {EntryPath}",
                        manifest.Id,
                        entryPath
                    );

                    continue;
                }

                _logger.Information("Executing Lua plugin {PluginId} entry {Entry}", manifest.Id, manifest.Entry);
                ExecuteScriptFile(entryPath);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to load Lua plugin manifest from {ManifestPath}", manifestPath);
            }
        }
    }

    /// <summary>
    /// Executes a script string with an optional file name for error reporting.
    /// </summary>
    /// <param name="script">The script to execute.</param>
    /// <param name="fileName">Optional file name for error reporting.</param>
    private void ExecuteScript(string script, string? fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(script);

        var stopwatch = Stopwatch.GetTimestamp();

        try
        {
            var compiledScriptChunk = _scriptCache.GetOrAddCompiledChunk(
                script,
                fileName,
                () => LuaScript.LoadString(script, null, fileName ?? "runtime_chunk")
            );

            lock (_scriptExecutionSync)
            {
                LuaScript.Call(compiledScriptChunk);
            }
            var elapsedMs = Stopwatch.GetElapsedTime(stopwatch);
            _logger.Debug("Script executed successfully in {ElapsedMs}ms", elapsedMs);
        }
        catch (ScriptRuntimeException luaEx)
        {
            var errorInfo = LuaReflectionHelper.CreateErrorInfo(luaEx, script, fileName);
            OnScriptError?.Invoke(this, errorInfo);

            _logger.Error(
                luaEx,
                "Lua error at line {Line}, column {Column}: {Message}",
                errorInfo.LineNumber,
                errorInfo.ColumnNumber,
                errorInfo.Message
            );

            throw;
        }
        catch (InterpreterException luaEx)
        {
            var errorInfo = LuaReflectionHelper.CreateErrorInfo(luaEx, script, fileName);
            OnScriptError?.Invoke(this, errorInfo);

            _logger.Error(
                luaEx,
                "Lua {ErrorType} at line {Line}, column {Column}: {Message}",
                errorInfo.ErrorType,
                errorInfo.LineNumber,
                errorInfo.ColumnNumber,
                errorInfo.Message
            );

            throw;
        }
        catch (Exception e)
        {
            var elapsedMs = Stopwatch.GetElapsedTime(stopwatch);
            _logger.Error(
                e,
                "Error executing script: {ScriptPreview}",
                script.Length > 100 ? script[..100] + "..." : script
            );

            throw;
        }
    }

    [RequiresUnreferencedCode(
        "Lua meta generation relies on reflection-heavy LuaDocumentationGenerator which is not trim-safe."
    )]
    private async Task GenerateLuaMetaFileAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.Debug("Generating Lua meta files");

            var definitionDirectory = _engineConfig.LuarcDirectory;

            if (!Directory.Exists(definitionDirectory))
            {
                Directory.CreateDirectory(definitionDirectory);
            }

            foreach (var userData in _loadedUserData)
            {
                // check if is enum

                if (userData.UserType.IsEnum)
                {
                    LuaDocumentationGenerator.FoundEnums.Add(userData.UserType);

                    continue;
                }

                LuaDocumentationGenerator.AddClassToGenerate(userData.UserType);
            }

            AddConstant("engine_version", _engineConfig.EngineVersion);

            // Generate meta.lua
            var manualModulesSnapshot = _manualModuleFunctions.ToDictionary(
                kvp => kvp.Key,
                IReadOnlyCollection<string> (kvp) => [.. kvp.Value.Keys]
            );

            var documentation = LuaDocumentationGenerator.GenerateDocumentation(
                "Moongate",
                _engineConfig.EngineVersion,
                _scriptModules,
                new(_constants),
                manualModulesSnapshot,
                _nameResolver
            );

            var metaLuaPath = Path.Combine(definitionDirectory, "definitions.lua");
            await File.WriteAllTextAsync(metaLuaPath, documentation, cancellationToken);
            _logger.Debug("Lua meta file generated at {Path}", metaLuaPath);

            // Generate .luarc.json
            var luarcJson = GenerateLuarcJson();
            var luarcPath = Path.Combine(_engineConfig.LuarcDirectory, ".luarc.json");
            await File.WriteAllTextAsync(luarcPath, luarcJson, cancellationToken);
            _logger.Debug("Lua configuration file generated at {Path}", luarcPath);
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to generate Lua meta files");
        }
    }

    private string GenerateLuarcJson()
    {
        var globalsList = _constants.Keys.ToList();
        globalsList.AddRange(collection);

        // Add registered user data types (Vector3, Vector2, Quaternion, etc.)
        foreach (var userData in _loadedUserData)
        {
            globalsList.Add(userData.UserType.Name);
        }

        var workspaceLibrary = new List<string>
        {
            _engineConfig.ScriptsDirectory,
            _engineConfig.LuarcDirectory
        };

        if (!string.IsNullOrWhiteSpace(_engineConfig.PluginsDirectory))
        {
            workspaceLibrary.Add(_engineConfig.PluginsDirectory);
        }

        var luarcConfig = new LuarcConfig
        {
            Runtime = new()
            {
                Path =
                [
                    "?.lua",
                    "?/init.lua",
                    "modules/?.lua",
                    "modules/?/init.lua",
                    "plugin/?/?.lua",
                    "plugin/?/?/init.lua"
                ]
            },
            Workspace = new()
            {
                Library = [.. workspaceLibrary]
            },
            Diagnostics = new()
            {
                Globals = [..globalsList]
            }
        };

        return JsonSerializer.Serialize(luarcConfig, MoongateLuaScriptJsonContext.Default.LuarcConfig);
    }

    private static string? GetStringField(Table table, string key)
    {
        var dyn = table.Get(key);

        return dyn.Type == DataType.String ? dyn.String : null;
    }

    private void InitializeReputationTitles()
    {
        var reputationTitles = LuaScript.Globals.Get(ReputationTitlesGlobalName);

        if (reputationTitles.Type is DataType.Nil or DataType.Void)
        {
            ReputationTitleRuntime.Reset();
            _logger.Debug("Lua reputation titles config not defined; using default runtime table.");

            return;
        }

        if (reputationTitles.Type != DataType.Table || reputationTitles.Table is null)
        {
            ReputationTitleRuntime.Reset();
            _logger.Warning("Lua reputation titles config is not a table; using default runtime table.");

            return;
        }

        if (!ReputationTitleLuaParser.TryParse(reputationTitles.Table, out var configuration))
        {
            ReputationTitleRuntime.Reset();
            _logger.Warning("Lua reputation titles config is invalid; using default runtime table.");

            return;
        }

        ReputationTitleRuntime.Configure(configuration);
        _logger.Information("Lua reputation titles config loaded successfully.");
    }

    private static bool IsSimpleType(Type type)
        => type.IsPrimitive || type == typeof(string) || type.IsEnum;

    private LuaPluginManifest? LoadPluginManifest(string manifestPath)
    {
        var content = File.ReadAllText(manifestPath);
        var manifestScript = new Script();
        var result = manifestScript.DoString(content, null, manifestPath);

        if (result.Type != DataType.Table)
        {
            _logger.Warning("Skipping Lua plugin manifest at {ManifestPath}: manifest did not return a table", manifestPath);

            return null;
        }

        var table = result.Table;

        return new(
            GetStringField(table, "id"),
            GetStringField(table, "name"),
            GetStringField(table, "version"),
            GetStringField(table, "entry")
        );
    }

    private void LoadToUserData()
    {
        if (_loadedUserData == null)
        {
            return;
        }

        foreach (var scriptUserData in _loadedUserData)
        {
            // Register the type to allow MoonSharp to access its members and methods
            UserData.RegisterType(scriptUserData.UserType, new GenericUserDataDescriptor(scriptUserData.UserType));

            // Check if type has public constructors (instantiable)
        #pragma warning disable IL2075 // Suppress AOT warning for script proxy
            var publicConstructors = scriptUserData.UserType.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
        #pragma warning restore IL2075

            if (publicConstructors.Length > 0)
            {
                // Instantiable type - use constructor wrapper for easier instance creation
                var constructorWrapper = LuaReflectionHelper.CreateConstructorWrapper(scriptUserData.UserType);
                LuaScript.Globals[scriptUserData.UserType.Name] = constructorWrapper;
            }
            else
            {
                // Static class or no public constructors - expose the type itself for static method access
                LuaScript.Globals[scriptUserData.UserType.Name] = scriptUserData.UserType;
            }

            _logger.Debug("User data type registered: {TypeName}", scriptUserData.UserType.Name);

            LuaDocumentationGenerator.AddClassToGenerate(scriptUserData.UserType);
        }
    }

    private (string ModuleName, string FunctionName, Table ModuleTable) PrepareManualModule(
        string moduleName,
        string functionName
    )
    {
        var normalizedModuleName = _nameResolver(moduleName);
        var normalizedFunctionName = _nameResolver(functionName);

        var existing = LuaScript.Globals.Get(normalizedModuleName);
        Table moduleTable;

        if (existing.Type == DataType.Table)
        {
            moduleTable = existing.Table;
        }
        else
        {
            moduleTable = new(LuaScript);
            LuaScript.Globals[normalizedModuleName] = moduleTable;
        }

        _loadedModules.TryAdd(normalizedModuleName, moduleTable);

        return (normalizedModuleName, normalizedFunctionName, moduleTable);
    }

    private void RegisterManualModuleFunction(string moduleName, string functionName)
    {
        var functions = _manualModuleFunctions.GetOrAdd(moduleName, _ => new());
        functions.TryAdd(functionName, 0);
    }
}
