using System.Globalization;
using MoonSharp.Interpreter;

namespace Moongate.Server.Scripting;

/// <summary>
/// Resolves a Lua-supplied value to a C# enum, accepting either a member name (case-insensitive)
/// or a numeric value such as an exposed enum constant. Only defined enum members are accepted.
/// </summary>
internal static class ScriptEnums
{
    public static bool TryResolve<TEnum>(object? value, out TEnum result) where TEnum : struct, Enum
    {
        result = default;

        switch (value)
        {
            case string name when !string.IsNullOrWhiteSpace(name):
                return Enum.TryParse(name, ignoreCase: true, out result) && Enum.IsDefined(result);

            case IConvertible convertible when value is not string:
                {
                    var number = convertible.ToInt32(CultureInfo.InvariantCulture);
                    var converted = (TEnum)Enum.ToObject(typeof(TEnum), number);

                    if (!Enum.IsDefined(converted))
                    {
                        return false;
                    }

                    result = converted;

                    return true;
                }

            default:
                return false;
        }
    }

    public static bool TryResolve<TEnum>(DynValue value, out TEnum result) where TEnum : struct, Enum
        => TryResolve(ToClr(value), out result);

    private static object? ToClr(DynValue value)
        => value.Type switch
        {
            DataType.String => value.String,
            DataType.Number => value.Number,
            _               => null
        };
}
