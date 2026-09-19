using System.Reflection;
using System.Text;
using Lua;
using Moongate.Scripting.Attributes.Scripts;
using Moongate.Scripting.Data.Binding;
using Moongate.Scripting.Interfaces;
using Moongate.Scripting.Internal;

namespace Moongate.Scripting.Binding;

/// <summary>Turns a [ScriptModule] instance into a Lua table. Reflection runs here, once; the delegates it builds never reflect.</summary>
public sealed class LuaModuleBinder
{
    private readonly IScriptThreadGuard _guard;
    private readonly List<Type> _discoveredEnums = [];
    private readonly List<Type> _publishedEnums = [];

    /// <summary>Gets every enum type seen so far as a parameter or return type of a bound function, in the order they were first encountered.</summary>
    public IReadOnlyList<Type> DiscoveredEnums => _discoveredEnums;

    /// <summary>Gets every enum type published as a global table via <see cref="BindEnum"/>, in the order it was published.</summary>
    public IReadOnlyList<Type> PublishedEnums => _publishedEnums;

    /// <summary>Initializes a new instance of the <see cref="LuaModuleBinder"/> class.</summary>
    /// <param name="guard">The thread guard called before every bound function invocation.</param>
    public LuaModuleBinder(IScriptThreadGuard guard)
    {
        _guard = guard;
    }

    /// <summary>Reflects <paramref name="moduleInstance"/> once, publishing a read-only Lua table under its [ScriptModule] name with one LuaFunction per [ScriptFunction] method.</summary>
    /// <param name="state">The Lua state to publish the module's table into.</param>
    /// <param name="moduleInstance">The [ScriptModule]-attributed instance to bind.</param>
    /// <returns>The bound module: its Lua table, and every function and constant published on it.</returns>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="moduleInstance"/>'s type carries no [ScriptModule], two of its [ScriptFunction] methods resolve to the same Lua name, or a method's signature uses a parameter or return type the converter cannot bind.
    /// </exception>
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

        var constants = BindConstants(moduleType, moduleAttribute.Name, hidden, seen);

        var table = ReadOnlyTable.Wrap(hidden, moduleAttribute.Name);
        state.Environment[moduleAttribute.Name] = new LuaValue(table);

        return new BoundModule(moduleAttribute.Name, moduleAttribute.HelpText, moduleType, table, functions, constants);
    }

    /// <summary>Publishes an enum as a read-only global table named after the type, keyed by member name with numeric values.</summary>
    /// <param name="state">The Lua state to publish the enum's table into.</param>
    /// <param name="enumType">The enum type to publish.</param>
    /// <exception cref="ArgumentException"><paramref name="enumType"/> is not an enum.</exception>
    public void BindEnum(LuaState state, Type enumType)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(enumType);

        if (!enumType.IsEnum)
        {
            throw new ArgumentException($"{enumType.FullName} is not an enum.", nameof(enumType));
        }

        var hidden = new LuaTable();

        foreach (var name in Enum.GetNames(enumType))
        {
            var value = Convert.ToDouble(Enum.Parse(enumType, name), System.Globalization.CultureInfo.InvariantCulture);
            hidden[name] = new LuaValue(value);
        }

        state.Environment[enumType.Name] = new LuaValue(ReadOnlyTable.Wrap(hidden, enumType.Name));
        NoteEnum(enumType);

        if (!_publishedEnums.Contains(enumType))
        {
            _publishedEnums.Add(enumType);
        }
    }

    private List<BoundConstant> BindConstants(Type moduleType, string moduleName, LuaTable hidden, HashSet<string> seen)
    {
        var constants = new List<BoundConstant>();
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;

        foreach (var member in moduleType.GetMembers(flags))
        {
            var attribute = member.GetCustomAttribute<ScriptConstantAttribute>(inherit: false);

            if (attribute is null)
            {
                continue;
            }

            var (type, isStaticReadOnly) = member switch
            {
                FieldInfo field => (field.FieldType, field.IsStatic && field.IsInitOnly && field.IsPublic),
                PropertyInfo property => (property.PropertyType, property.GetMethod is { IsStatic: true, IsPublic: true } && property.SetMethod is null),
                _ => (typeof(void), false)
            };

            if (!isStaticReadOnly)
            {
                throw new InvalidOperationException(
                    $"{moduleType.FullName}.{member.Name}: a [ScriptConstant] must be a public static readonly field or a public static get-only property.");
            }

            if (type == typeof(LuaTable) || type == typeof(LuaValue) || type == typeof(object) || !LuaValueConverter.IsSupported(type))
            {
                throw new InvalidOperationException(
                    $"{moduleType.FullName}.{member.Name}: constants of type {type.Name} are not supported; use int, long, double, bool, string or an enum.");
            }

            var luaName = attribute.Name ?? member.Name;

            if (!seen.Add(luaName))
            {
                throw new InvalidOperationException(
                    $"{moduleType.FullName}.{member.Name}: Lua name '{luaName}' is already used in module '{moduleName}'.");
            }

            var value = ReadConstant(moduleType, member);
            NoteEnum(type);
            hidden[luaName] = LuaValueConverter.ToLua(value, type);
            constants.Add(new BoundConstant(luaName, type, value, attribute.HelpText));
        }

        return constants;
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

    /// <summary>Reads a validated constant; a getter that throws becomes a binding error naming the member, with the getter's exception as the cause.</summary>
    private static object? ReadConstant(Type moduleType, MemberInfo member)
    {
        try
        {
            return member switch
            {
                FieldInfo field => field.GetValue(null),
                PropertyInfo property => property.GetValue(null),
                _ => null
            };
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            throw new InvalidOperationException(
                $"{moduleType.FullName}.{member.Name}: the constant's getter threw {exception.InnerException.GetType().Name}: {exception.InnerException.Message}",
                exception.InnerException);
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
