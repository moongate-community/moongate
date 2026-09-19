using Lua;

namespace Moongate.Scripting.Internal;

/// <summary>The one place that decides how CLR values and Lua values map onto each other.</summary>
internal static class LuaValueConverter
{
    public static bool IsSupported(Type type)
    {
        if (type.IsEnum)
        {
            return true;
        }

        return type == typeof(int) || type == typeof(long) || type == typeof(double) || type == typeof(float) ||
               type == typeof(bool) || type == typeof(string) || type == typeof(LuaTable) || type == typeof(LuaValue) ||
               type == typeof(object) || type == typeof(void);
    }

    public static LuaValue ToLua(object? value, Type declaredType)
    {
        if (value is null)
        {
            return LuaValue.Nil;
        }

        if (declaredType.IsEnum || value.GetType().IsEnum)
        {
            return new LuaValue(Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture));
        }

        return value switch
        {
            LuaValue lua => lua,
            LuaTable table => new LuaValue(table),
            string text => new LuaValue(text),
            bool flag => new LuaValue(flag),
            int number => new LuaValue(number),
            long number => new LuaValue(number),
            float number => new LuaValue(number),
            double number => new LuaValue(number),
            _ => throw new InvalidCastException($"Values of type {value.GetType().FullName} cannot be passed to Lua.")
        };
    }

    public static object? FromLua(LuaValue value, Type targetType)
    {
        if (targetType == typeof(LuaValue))
        {
            return value;
        }

        if (value.Type == LuaValueType.Nil)
        {
            return targetType.IsValueType && Nullable.GetUnderlyingType(targetType) is null
                ? throw new InvalidCastException($"nil cannot be converted to {targetType.Name}")
                : null;
        }

        var underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (underlying.IsEnum)
        {
            return ReadEnum(value, underlying);
        }

        if (underlying == typeof(object))
        {
            // Each arm is cast to object explicitly. LuaValue defines implicit conversions from
            // double/string/bool/LuaTable, so without the casts the switch expression's inferred
            // common type would be LuaValue (not object): every arm would convert to LuaValue first,
            // and the method would hand back a boxed LuaValue instead of the unwrapped CLR value.
            return value.Type switch
            {
                LuaValueType.Number => (object)value.Read<double>(),
                LuaValueType.String => (object)value.Read<string>(),
                LuaValueType.Boolean => (object)value.Read<bool>(),
                LuaValueType.Table => (object)value.Read<LuaTable>(),
                _ => value
            };
        }

        if (underlying == typeof(string))
        {
            return Expect(value, LuaValueType.String, underlying).Read<string>();
        }

        if (underlying == typeof(bool))
        {
            return Expect(value, LuaValueType.Boolean, underlying).Read<bool>();
        }

        if (underlying == typeof(LuaTable))
        {
            return Expect(value, LuaValueType.Table, underlying).Read<LuaTable>();
        }

        var number = Expect(value, LuaValueType.Number, underlying).Read<double>();

        if (underlying == typeof(double))
        {
            return number;
        }

        if (underlying == typeof(float))
        {
            return (float)number;
        }

        if (!double.IsInteger(number))
        {
            throw new InvalidCastException($"{number} has no integer representation for {underlying.Name}");
        }

        if (underlying == typeof(int))
        {
            return checked((int)number);
        }

        if (underlying == typeof(long))
        {
            return checked((long)number);
        }

        throw new InvalidCastException($"Parameters of type {targetType.FullName} are not supported.");
    }

    private static object ReadEnum(LuaValue value, Type enumType)
    {
        if (value.Type == LuaValueType.Number)
        {
            var number = value.Read<double>();

            if (!double.IsInteger(number))
            {
                throw new InvalidCastException($"{number} is not a member of {enumType.Name}");
            }

            var boxed = Enum.ToObject(enumType, (long)number);

            if (!Enum.IsDefined(enumType, boxed))
            {
                throw new InvalidCastException($"{number} is not a member of {enumType.Name}");
            }

            return boxed;
        }

        if (value.Type == LuaValueType.String)
        {
            var name = value.Read<string>();

            if (Enum.TryParse(enumType, name, ignoreCase: false, out var parsed) && parsed is not null)
            {
                return parsed;
            }

            throw new InvalidCastException($"'{name}' is not a member of {enumType.Name}");
        }

        throw new InvalidCastException($"{enumType.Name} expected, got {value.TypeToString()}");
    }

    private static LuaValue Expect(LuaValue value, LuaValueType expected, Type target)
    {
        if (value.Type != expected)
        {
            throw new InvalidCastException($"{target.Name} expected, got {value.TypeToString()}");
        }

        return value;
    }
}
