using System.Globalization;

namespace Moongate.Server.Ultima.Entities.Internal;

/// <summary>
///     The rules of an entity's <c>props</c> JSONB dictionary, shared by items and mobiles: strings, numbers, bools and
///     enums only; a stored value converts to the type asked for; an empty dictionary is stored as null.
/// </summary>
internal static class PropsDictionary
{
    public static Dictionary<string, object?>? Set(Dictionary<string, object?>? props, string key, object? value)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("A prop needs a key.", nameof(key));
        }

        if (value is null)
        {
            return Remove(props, key, out _);
        }

        if (value is not (string or
            bool or
            Enum or
            byte or
            sbyte or
            short or
            ushort or
            int or
            uint or
            long or
            ulong or
            float or
            double or
            decimal))
        {
            throw new ArgumentException(
                $"Prop '{key}' cannot hold a {value.GetType().Name}: use a string, a number, a bool or an enum.",
                nameof(value)
            );
        }

        props ??= new();
        props[key] = value;

        return props;
    }

    public static bool TryGet<T>(Dictionary<string, object?>? props, string key, out T value)
    {
        if (props is null || !props.TryGetValue(key, out var stored) || stored is null)
        {
            value = default!;

            return false;
        }

        value = Convert<T>(key, stored);

        return true;
    }

    public static Dictionary<string, object?>? Remove(Dictionary<string, object?>? props, string key, out bool removed)
    {
        removed = props is not null && props.Remove(key);

        return props is { Count: 0 } ? null : props;
    }

    // The JSONB column gives whole numbers back as long and enums as their number, so a stored value is converted to
    // the type asked for rather than cast.
    private static T Convert<T>(string key, object stored)
    {
        if (stored is T typed)
        {
            return typed;
        }

        var target = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);

        try
        {
            var converted = target.IsEnum
                ? stored is string name
                    ? Enum.Parse(target, name, true)
                    : Enum.ToObject(target, System.Convert.ToInt64(stored, CultureInfo.InvariantCulture))
                : System.Convert.ChangeType(stored, target, CultureInfo.InvariantCulture);

            return (T)converted;
        }
        catch (Exception exception) when (exception is FormatException or
                                              InvalidCastException or
                                              OverflowException or
                                              ArgumentException)
        {
            throw new InvalidCastException(
                $"Prop '{key}' holds {stored} ({stored.GetType().Name}), which is not a {typeof(T).Name}.",
                exception
            );
        }
    }
}
