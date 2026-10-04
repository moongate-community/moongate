using System.Globalization;
using System.Reflection;
using System.Text;
using Lua;
using Moongate.Scripting.Data.Binding;
using Moongate.Scripting.Interfaces;
using Moongate.Scripting.Internal;

namespace Moongate.Scripting.Binding;

/// <summary>
///     Turns a [ScriptModule] instance into a Lua table. Reflection runs here, once; the delegates it builds never
///     reflect.
/// </summary>
public sealed class LuaModuleBinder
{
    private readonly IScriptThreadGuard _guard;
    private readonly List<Type> _discoveredEnums = [];
    private readonly List<Type> _publishedEnums = [];

    /// <summary>
    ///     Gets every enum type seen so far as a parameter or return type of a bound function, in the order they were first
    ///     encountered.
    /// </summary>
    public IReadOnlyList<Type> DiscoveredEnums => _discoveredEnums;

    /// <summary>
    ///     Gets every enum type published as a global table via <see cref="BindEnum" />, in the order it was published.
    /// </summary>
    public IReadOnlyList<Type> PublishedEnums => _publishedEnums;

    /// <summary>
    ///     Initializes a new instance of the <see cref="LuaModuleBinder" /> class.
    /// </summary>
    /// <param name="guard">
    ///     The thread guard called before every bound function invocation.
    /// </param>
    public LuaModuleBinder(IScriptThreadGuard guard)
    {
        _guard = guard;
    }

    /// <summary>
    ///     Reflects <paramref name="moduleInstance" /> once, publishing a read-only Lua table under its [ScriptModule] name
    ///     with one LuaFunction per [ScriptFunction] method.
    /// </summary>
    /// <param name="state">
    ///     The Lua state to publish the module's table into.
    /// </param>
    /// <param name="moduleInstance">
    ///     The [ScriptModule]-attributed instance to bind.
    /// </param>
    /// <returns>
    ///     The bound module: its Lua table, and every function and constant published on it.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    ///     <paramref name="moduleInstance" />'s type carries no [ScriptModule], a [ScriptFunction] sits on a static, non-public
    ///     or generic method, two of its [ScriptFunction] methods resolve to the same Lua name, or a method's signature uses a
    ///     parameter or return type the converter cannot bind.
    /// </exception>
    public BoundModule Bind(LuaState state, object moduleInstance)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(moduleInstance);
        var description = LuaModuleDescriber.Describe(moduleInstance.GetType());
        var hidden = new LuaTable();

        foreach (var function in description.Functions)
        {
            hidden[function.LuaName] = new(
                CreateFunction(description.Name, function.LuaName, moduleInstance, function.Method)
            );
        }

        foreach (var constant in description.Constants)
        {
            hidden[constant.LuaName] = LuaValueConverter.ToLua(constant.Value, constant.Type);
        }

        foreach (var enumType in LuaModuleDescriber.EnumsOf(description))
        {
            NoteEnum(enumType);
        }

        var table = ReadOnlyTable.Wrap(hidden, description.Name);
        state.Environment[description.Name] = new(table);

        return new(
            description.Name,
            description.HelpText,
            description.ModuleType,
            table,
            description.Functions,
            description.Constants
        );
    }

    /// <summary>
    ///     Publishes an enum as a read-only global table named after the type, keyed by member name with numeric values.
    /// </summary>
    /// <param name="state">
    ///     The Lua state to publish the enum's table into.
    /// </param>
    /// <param name="enumType">
    ///     The enum type to publish.
    /// </param>
    /// <exception cref="ArgumentException">
    ///     <paramref name="enumType" /> is not an enum.
    /// </exception>
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
            var value = Convert.ToDouble(Enum.Parse(enumType, name), CultureInfo.InvariantCulture);
            hidden[name] = new(value);
        }

        state.Environment[enumType.Name] = new(ReadOnlyTable.Wrap(hidden, enumType.Name));
        NoteEnum(enumType);

        if (!_publishedEnums.Contains(enumType))
        {
            _publishedEnums.Add(enumType);
        }
    }

    internal static string ToSnakeCase(string name)
    {
        var builder = new StringBuilder(name.Length + 4);

        for (var i = 0; i < name.Length; i++)
        {
            var character = name[i];

            if (char.IsUpper(character))
            {
                if (i > 0 && (!char.IsUpper(name[i - 1]) || i + 1 < name.Length && char.IsLower(name[i + 1])))
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

    private LuaFunction CreateFunction(string moduleName, string luaName, object instance, MethodInfo method)
    {
        var qualified = moduleName + "." + luaName;
        var parameters = method.GetParameters();
        var hasParams = parameters.Length > 0 && parameters[^1].GetCustomAttribute<ParamArrayAttribute>() is not null;
        var fixedCount = hasParams ? parameters.Length - 1 : parameters.Length;
        var returnsVoid = method.ReturnType == typeof(void);

        return new(
            qualified,
            (context, _) =>
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
                            throw new LuaRuntimeException(
                                context.State,
                                new LuaValue($"bad argument #{i + 1} to '{qualified}' ({parameter.Name} is required)")
                            );
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
                        throw new LuaRuntimeException(
                            context.State,
                            new LuaValue($"bad argument #{i + 1} to '{qualified}' ({exception.Message})")
                        );
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
                            throw new LuaRuntimeException(
                                context.State,
                                new LuaValue($"bad argument #{fixedCount + i + 1} to '{qualified}' ({exception.Message})")
                            );
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
                    throw new LuaRuntimeException(
                        context.State,
                        new LuaValue($"'{qualified}' failed: {exception.InnerException.Message}")
                    );
                }

                return returnsVoid
                    ? new(context.Return())
                    : new ValueTask<int>(context.Return(LuaValueConverter.ToLua(result, method.ReturnType)));
            }
        );
    }

    private void NoteEnum(Type type)
    {
        if (type.IsEnum && !_discoveredEnums.Contains(type))
        {
            _discoveredEnums.Add(type);
        }
    }
}
