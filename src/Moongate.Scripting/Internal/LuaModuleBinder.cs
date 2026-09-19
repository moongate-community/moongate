using System.Reflection;
using System.Text;
using Lua;
using Moongate.Scripting.Attributes.Scripts;
using Moongate.Scripting.Interfaces;

namespace Moongate.Scripting.Internal;

/// <summary>Turns a [ScriptModule] instance into a Lua table. Reflection runs here, once; the delegates it builds never reflect.</summary>
internal sealed class LuaModuleBinder
{
    private readonly IScriptThreadGuard _guard;
    private readonly List<Type> _discoveredEnums = [];

    public IReadOnlyList<Type> DiscoveredEnums => _discoveredEnums;

    public LuaModuleBinder(IScriptThreadGuard guard)
    {
        _guard = guard;
    }

    public BoundModule Bind(LuaState state, object moduleInstance)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(moduleInstance);
        var moduleType = moduleInstance.GetType();
        var moduleAttribute = moduleType.GetCustomAttribute<ScriptModuleAttribute>(inherit: false)
                              ?? throw new InvalidOperationException($"{moduleType.FullName} carries no [ScriptModule].");
        var hidden = new LuaTable();
        var functions = new List<BoundFunction>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var method in moduleType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            var attribute = method.GetCustomAttribute<ScriptFunctionAttribute>(inherit: false);

            if (attribute is null)
            {
                continue;
            }

            var luaName = attribute.Name ?? ToSnakeCase(method.Name);

            if (!seen.Add(luaName))
            {
                throw new InvalidOperationException(
                    $"{moduleType.FullName}.{method.Name}: Lua name '{luaName}' is already used in module '{moduleAttribute.Name}'.");
            }

            ValidateSignature(moduleType, method);
            hidden[luaName] = new LuaValue(CreateFunction(moduleAttribute.Name, luaName, moduleInstance, method));
            functions.Add(new BoundFunction(luaName, method, attribute.HelpText));
        }

        var table = ReadOnlyTable.Wrap(hidden, moduleAttribute.Name);
        state.Environment[moduleAttribute.Name] = new LuaValue(table);

        return new BoundModule(moduleAttribute.Name, moduleAttribute.HelpText, moduleType, table, functions, []);
    }

    private void ValidateSignature(Type moduleType, MethodInfo method)
    {
        var parameters = method.GetParameters();

        for (var i = 0; i < parameters.Length; i++)
        {
            var parameter = parameters[i];
            var isParams = i == parameters.Length - 1 && parameter.GetCustomAttribute<ParamArrayAttribute>() is not null;
            var type = isParams ? parameter.ParameterType.GetElementType()! : parameter.ParameterType;
            type = Nullable.GetUnderlyingType(type) ?? type;

            if (!LuaValueConverter.IsSupported(type) || type == typeof(void))
            {
                throw new InvalidOperationException(
                    $"{moduleType.FullName}.{method.Name}: parameter '{parameter.Name}' of type {parameter.ParameterType.Name} cannot be bound.");
            }

            NoteEnum(type);
        }

        var returnType = Nullable.GetUnderlyingType(method.ReturnType) ?? method.ReturnType;

        if (!LuaValueConverter.IsSupported(returnType))
        {
            throw new InvalidOperationException(
                $"{moduleType.FullName}.{method.Name}: return type {method.ReturnType.Name} cannot be bound.");
        }

        NoteEnum(returnType);
    }

    private void NoteEnum(Type type)
    {
        if (type.IsEnum && !_discoveredEnums.Contains(type))
        {
            _discoveredEnums.Add(type);
        }
    }

    private LuaFunction CreateFunction(string moduleName, string luaName, object instance, MethodInfo method)
    {
        var qualified = moduleName + "." + luaName;
        var parameters = method.GetParameters();
        var hasParams = parameters.Length > 0 && parameters[^1].GetCustomAttribute<ParamArrayAttribute>() is not null;
        var fixedCount = hasParams ? parameters.Length - 1 : parameters.Length;
        var returnsVoid = method.ReturnType == typeof(void);

        return new LuaFunction(qualified, (context, _) =>
        {
            _guard.EnsureScriptThread(qualified);
            var arguments = new object?[parameters.Length];

            for (var i = 0; i < fixedCount; i++)
            {
                var parameter = parameters[i];

                if (!context.HasArgument(i))
                {
                    if (!parameter.HasDefaultValue)
                    {
                        throw new LuaRuntimeException(context.State,
                            new LuaValue($"bad argument #{i + 1} to '{qualified}' ({parameter.Name} is required)"), 1);
                    }

                    arguments[i] = parameter.DefaultValue;
                    continue;
                }

                try
                {
                    arguments[i] = LuaValueConverter.FromLua(context.GetArgument(i), parameter.ParameterType);
                }
                catch (InvalidCastException exception)
                {
                    throw new LuaRuntimeException(context.State,
                        new LuaValue($"bad argument #{i + 1} to '{qualified}' ({exception.Message})"), 1);
                }
            }

            if (hasParams)
            {
                var elementType = parameters[^1].ParameterType.GetElementType()!;
                var extra = Math.Max(0, context.ArgumentCount - fixedCount);
                var rest = Array.CreateInstance(elementType, extra);

                for (var i = 0; i < extra; i++)
                {
                    try
                    {
                        rest.SetValue(LuaValueConverter.FromLua(context.GetArgument(fixedCount + i), elementType), i);
                    }
                    catch (InvalidCastException exception)
                    {
                        throw new LuaRuntimeException(context.State,
                            new LuaValue($"bad argument #{fixedCount + i + 1} to '{qualified}' ({exception.Message})"), 1);
                    }
                }

                arguments[^1] = rest;
            }

            object? result;

            try
            {
                result = method.Invoke(instance, arguments);
            }
            catch (TargetInvocationException exception) when (exception.InnerException is not null)
            {
                throw new LuaRuntimeException(context.State,
                    new LuaValue($"'{qualified}' failed: {exception.InnerException.Message}"), 1);
            }

            return returnsVoid
                ? new ValueTask<int>(context.Return())
                : new ValueTask<int>(context.Return(LuaValueConverter.ToLua(result, method.ReturnType)));
        });
    }

    internal static string ToSnakeCase(string name)
    {
        var builder = new StringBuilder(name.Length + 4);

        for (var i = 0; i < name.Length; i++)
        {
            var character = name[i];

            if (char.IsUpper(character))
            {
                if (i > 0 && (!char.IsUpper(name[i - 1]) || (i + 1 < name.Length && char.IsLower(name[i + 1]))))
                {
                    builder.Append('_');
                }

                builder.Append(char.ToLowerInvariant(character));
            }
            else
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }
}
