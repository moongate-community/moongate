using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using DryIoc;
using Moongate.Scripting.Attributes.Scripts;
using Moongate.Scripting.Data.Internal;
using MoonSharp.Interpreter;
using Serilog;

namespace Moongate.Scripting.Internal;

/// <summary>
/// Registers C# script modules, enums, and global helpers into the Lua runtime.
/// </summary>
internal sealed class LuaModuleLoader
{
    private readonly Script _luaScript;
    private readonly ILogger _logger;
    private readonly Func<string, string> _nameResolver;
    private readonly IContainer _serviceProvider;

    public LuaModuleLoader(Script luaScript, IContainer serviceProvider, Func<string, string> nameResolver, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(luaScript);
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(nameResolver);
        ArgumentNullException.ThrowIfNull(logger);

        _luaScript = luaScript;
        _serviceProvider = serviceProvider;
        _nameResolver = nameResolver;
        _logger = logger;
    }

    public void RegisterEnums(IReadOnlyCollection<Type> enumTypes)
    {
        ArgumentNullException.ThrowIfNull(enumTypes);

        foreach (var enumType in enumTypes)
        {
            RegisterEnum(enumType);
        }
    }

    public void RegisterGlobalFunctions()
    {
        _luaScript.Globals["delay"] = (Func<int, Task>)(async milliseconds =>
                                                        {
                                                            await Task.Delay(Math.Min(milliseconds, 5000));
                                                        });

        var existingLog = _luaScript.Globals.Get("log");

        if (existingLog.Type == DataType.Nil)
        {
            _luaScript.Globals["log"] = (Action<object>)(message => { _logger.Information("Lua: {Message}", message); });
        }
        else
        {
            _luaScript.Globals["log_message"] =
                (Action<object>)(message => { _logger.Information("Lua: {Message}", message); });
        }

        _luaScript.Globals["toString"] = (Func<object, string>)(obj => obj?.ToString() ?? "nil");
    }

    public void RegisterScriptModules(
        IReadOnlyList<ScriptModuleData> scriptModules,
        IDictionary<string, object> loadedModules,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(scriptModules);
        ArgumentNullException.ThrowIfNull(loadedModules);

        loadedModules.Clear();

        foreach (var module in scriptModules)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var scriptModuleAttribute = module.ModuleType.GetCustomAttribute<ScriptModuleAttribute>();

            if (scriptModuleAttribute is null)
            {
                continue;
            }

            if (!_serviceProvider.IsRegistered(module.ModuleType))
            {
                _serviceProvider.Register(module.ModuleType, Reuse.Singleton);
            }

            var instance = _serviceProvider.GetService(module.ModuleType);

            if (instance is null)
            {
                throw new InvalidOperationException($"Unable to create instance of script module {module.ModuleType.Name}");
            }

            var moduleName = scriptModuleAttribute.Name;
            _logger.Debug("Registering script module {Name}", moduleName);

            UserData.RegisterType(module.ModuleType, InteropAccessMode.Reflection);

            var moduleTable = CreateModuleTable(instance, module.ModuleType);
            _luaScript.Globals[moduleName] = moduleTable;

            loadedModules[moduleName] = instance;
        }
    }

    private Table CreateModuleTable(
        object instance,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] Type moduleType
    )
    {
        var moduleTable = new Table(_luaScript);

        var methods = moduleType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                                .Where(method => method.GetCustomAttribute<ScriptFunctionAttribute>() is not null);

        foreach (var method in methods)
        {
            var scriptFunctionAttribute = method.GetCustomAttribute<ScriptFunctionAttribute>();

            if (scriptFunctionAttribute is null)
            {
                continue;
            }

            var functionName = string.IsNullOrWhiteSpace(scriptFunctionAttribute.FunctionName)
                                   ? _nameResolver(method.Name)
                                   : scriptFunctionAttribute.FunctionName;

            var closure = LuaReflectionHelper.CreateMethodClosure(_logger, _luaScript, instance, method);
            moduleTable[functionName] = closure;
        }

        return moduleTable;
    }

    private void RegisterEnum(Type enumType)
    {
        ArgumentNullException.ThrowIfNull(enumType);

        if (!enumType.IsEnum)
        {
            _logger.Warning("Type {TypeName} is not an enum, skipping registration", enumType.Name);

            return;
        }

        var enumName = _nameResolver(enumType.Name);
        var enumTable = new Table(_luaScript);
        var enumValuesByName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var names = Enum.GetNames(enumType);
        var underlyingValues = Enum.GetValuesAsUnderlyingType(enumType);

        for (var i = 0; i < names.Length; i++)
        {
            var name = names[i];
            var rawValue = underlyingValues.GetValue(i);

            if (rawValue is null)
            {
                continue;
            }

            var coercedValue = Convert.ToInt32(rawValue, CultureInfo.InvariantCulture);
            enumTable[name] = coercedValue;
            enumValuesByName[name] = coercedValue;
        }

        var metatable = new Table(_luaScript);
        metatable["__index"] = DynValue.NewCallback(
            (_, args) =>
            {
                var key = args[1].String;

                if (string.IsNullOrEmpty(key))
                {
                    return DynValue.Nil;
                }

                var exactValue = enumTable.Get(key);

                if (exactValue.Type != DataType.Nil)
                {
                    return exactValue;
                }

                if (enumValuesByName.TryGetValue(key, out var intValue))
                {
                    return DynValue.NewNumber(intValue);
                }

                _logger.Warning("Attempt to access undefined enum value {EnumName}.{ValueName}", enumName, key);

                return DynValue.Nil;
            }
        );

        metatable["__newindex"] = DynValue.NewCallback(
            (_, args) =>
            {
                var key = args[1].String;

                throw new ScriptRuntimeException($"Cannot modify enum {enumName}.{key}: enums are read-only");
            }
        );

        metatable["__tostring"] = DynValue.NewCallback((_, _) => DynValue.NewString($"enum<{enumName}>"));

        try
        {
            enumTable.MetaTable = metatable;
        }
        catch
        {
            _logger.Warning("Could not apply metatable to enum {EnumName}, using fallback", enumName);
        }

        _luaScript.Globals[enumName] = DynValue.NewTable(enumTable);

        _logger.Debug(
            "Registered enum {EnumName} with {ValueCount} values (read-only, case-insensitive)",
            enumName,
            enumValuesByName.Count
        );
    }
}
