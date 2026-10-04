using System.Globalization;
using System.Reflection;
using System.Text;
using Lua;
using Moongate.Scripting.Attributes.Scripts;
using Moongate.Scripting.Data.Binding;

namespace Moongate.Scripting.Binding;

/// <summary>
///     Puts a script function's signature in Lua terms: parameter names, Lua types, what may be left out and what comes
///     back. The editor definitions and the documentation both read it, so they cannot disagree.
/// </summary>
public static class LuaFunctionDescriber
{
    /// <summary>
    ///     Describes one function of a module.
    /// </summary>
    /// <param name="function">
    ///     A function read by <see cref="LuaModuleDescriber" />.
    /// </param>
    /// <returns>
    ///     Its Lua name, help text, parameters and return type.
    /// </returns>
    public static FunctionDescription Describe(BoundFunction function)
    {
        ArgumentNullException.ThrowIfNull(function);
        var parameters = new List<ParameterDescription>();

        foreach (var parameter in function.Method.GetParameters())
        {
            var isParams = parameter.GetCustomAttribute<ParamArrayAttribute>() is not null;
            var type = isParams ? parameter.ParameterType.GetElementType()! : parameter.ParameterType;
            var underlying = Nullable.GetUnderlyingType(type) ?? type;
            var optional = !isParams && (parameter.HasDefaultValue || IsNullableValueType(parameter.ParameterType));

            // The converter also accepts an enum member by name, so a script may pass the string; returns
            // stay the bare enum because the engine always hands back the number.
            // A declared Lua type (such as the EventName alias) wins over the one derived from the CLR type.
            var typeName = parameter.GetCustomAttribute<ScriptParameterTypeAttribute>()?.LuaType ??
                           (underlying.IsEnum ? LuaTypeName(type) + "|string" : LuaTypeName(type));
            parameters.Add(new(isParams ? "..." : LuaModuleBinder.ToSnakeCase(parameter.Name!), typeName, optional, DefaultOf(parameter, underlying)));
        }

        string? returns = null;

        if (function.Method.ReturnType != typeof(void))
        {
            var returnType = function.Method.ReturnType;
            var nullableReference = !returnType.IsValueType &&
                                    new NullabilityInfoContext().Create(function.Method.ReturnParameter).ReadState ==
                                    NullabilityState.Nullable;
            returns = LuaTypeName(returnType) + (IsNullableValueType(returnType) || nullableReference ? "?" : "");
        }

        return new(function.LuaName, function.HelpText, parameters, returns);
    }

    /// <summary>
    ///     Writes a value as one Lua token: nil, a quoted string, true or false, a number, or an enum's number.
    /// </summary>
    /// <param name="value">
    ///     The value; null gives <c>nil</c>.
    /// </param>
    /// <param name="type">
    ///     The value's declared CLR type.
    /// </param>
    public static string LuaLiteral(object? value, Type type)
    {
        return value switch
        {
            null        => "nil",
            string text => "\"" + EscapeLuaString(text) + "\"",
            bool flag   => flag ? "true" : "false",
            _ when type.IsEnum => Convert.ToInt64(value, CultureInfo.InvariantCulture)
                .ToString(CultureInfo.InvariantCulture),
            double number       => DoubleLiteral(number),
            float number        => FloatLiteral(number),
            IFormattable number => number.ToString(null, CultureInfo.InvariantCulture),
            _                   => "nil"
        };
    }

    /// <summary>
    ///     Names the Lua type scripts see for a CLR type: integer, number, boolean, string, table, an enum's name, or
    ///     any. A nullable value type gives the name of its underlying type.
    /// </summary>
    /// <param name="type">
    ///     The CLR type of a parameter, a return or a constant.
    /// </param>
    public static string LuaTypeName(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;

        if (underlying.IsEnum)
        {
            return underlying.Name;
        }

        if (underlying == typeof(int) || underlying == typeof(long))
        {
            return "integer";
        }

        if (underlying == typeof(double) || underlying == typeof(float))
        {
            return "number";
        }

        if (underlying == typeof(bool))
        {
            return "boolean";
        }

        if (underlying == typeof(string))
        {
            return "string";
        }

        if (underlying == typeof(LuaTable))
        {
            return "table";
        }

        return "any";
    }

    /// <summary>
    ///     The Lua token of a parameter's default, or null when leaving the argument out gives nil. An enum default is
    ///     written as its member, <c>DirectionType.North</c>, which is how a script would write it.
    /// </summary>
    private static string? DefaultOf(ParameterInfo parameter, Type underlying)
    {
        if (!parameter.HasDefaultValue || parameter.DefaultValue is null)
        {
            return null;
        }

        if (underlying.IsEnum)
        {
            var member = Enum.GetName(underlying, Enum.ToObject(underlying, parameter.DefaultValue));

            return member is null
                ? LuaLiteral(parameter.DefaultValue, underlying)
                : underlying.Name + "." + member;
        }

        return LuaLiteral(parameter.DefaultValue, underlying);
    }

    /// <summary>
    ///     Renders a double as a Lua token, mapping the three non-finite values to expressions Lua accepts (it has no numeric
    ///     literal for any of them).
    /// </summary>
    private static string DoubleLiteral(double value)
    {
        if (!double.IsFinite(value))
        {
            return NonFiniteLiteral(value);
        }

        return value.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    ///     Escapes a string constant so it is a single valid Lua string token: backslash, quote and every control character.
    /// </summary>
    private static string EscapeLuaString(string text)
    {
        var builder = new StringBuilder(text.Length);

        foreach (var character in text)
        {
            switch (character)
            {
                case '\\':
                    builder.Append("\\\\");

                    break;
                case '"':
                    builder.Append("\\\"");

                    break;
                case '\n':
                    builder.Append("\\n");

                    break;
                case '\r':
                    builder.Append("\\r");

                    break;
                case '\t':
                    builder.Append("\\t");

                    break;
                default:
                    if (char.IsControl(character))
                    {
                        // Lua's \ddd escape greedily consumes up to three following decimal digits, so an
                        // unpadded code (e.g. \7 before a literal "1") would read back as a different escape.
                        // Always emitting three digits keeps every following character its own token.
                        builder.Append('\\').Append(((int)character).ToString("D3", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        builder.Append(character);
                    }

                    break;
            }
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Renders a float as it was declared: widened to a double first, <c>0.1f</c> would read 0.10000000149011612.
    /// </summary>
    private static string FloatLiteral(float value)
    {
        return float.IsFinite(value) ? value.ToString(CultureInfo.InvariantCulture) : NonFiniteLiteral(value);
    }

    private static bool IsNullableValueType(Type type)
    {
        return Nullable.GetUnderlyingType(type) is not null;
    }

    private static string NonFiniteLiteral(double value)
    {
        if (double.IsNaN(value))
        {
            return "0/0";
        }

        return double.IsPositiveInfinity(value) ? "math.huge" : "-math.huge";
    }
}
