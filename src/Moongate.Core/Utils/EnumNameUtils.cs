namespace Moongate.Core.Utils;

/// <summary>
///     Writes and reads enum values as their snake_case names, the text form of every enum in Moongate's TOML files.
/// </summary>
/// <remarks>
///     A <see cref="FlagsAttribute" /> enum writes a value that has its own name as that name, and any other
///     combination as names joined by <c>|</c>, such as <c>"impassable|surface"</c>; zero without a name is an empty
///     string. Reading ignores case and underscores and accepts only names, never numbers.
/// </remarks>
public static class EnumNameUtils
{
    /// <summary>
    ///     Writes <paramref name="value" /> as its snake_case name, or as names joined by <c>|</c> for a combination of
    ///     flags.
    /// </summary>
    /// <exception cref="ArgumentException">
    ///     The value has no name, such as a number no member of the enum has.
    /// </exception>
    public static string Format<TEnum>(TEnum value) where TEnum : struct, Enum
    {
        // Enum.ToString names a defined value, splits other flags into the largest named parts ("Running, SouthEast"),
        // and falls back to the number when it cannot.
        var text = value.ToString();

        if (text.Length > 0 && (char.IsAsciiDigit(text[0]) || text[0] == '-'))
        {
            if (IsFlags<TEnum>() && EqualityComparer<TEnum>.Default.Equals(value, default))
            {
                return string.Empty;
            }

            throw new ArgumentException($"{text} is not a named {typeof(TEnum).Name} value.", nameof(value));
        }

        return string.Join('|', text.Split(", ").Select(StringUtils.ToSnakeCase));
    }

    /// <summary>
    ///     Reads a name written by <see cref="Format{TEnum}" />, ignoring case and underscores; a
    ///     <see cref="FlagsAttribute" /> enum also takes names joined by <c>|</c>, and an empty string for zero.
    /// </summary>
    /// <returns>
    ///     False, with <paramref name="value" /> left default, when the text is not a name of the enum.
    /// </returns>
    public static bool TryParse<TEnum>(string? text, out TEnum value) where TEnum : struct, Enum
    {
        value = default;

        if (text is null)
        {
            return false;
        }

        var flags = IsFlags<TEnum>();

        if (string.IsNullOrWhiteSpace(text))
        {
            return flags;
        }

        var parts = text.Split('|', StringSplitOptions.TrimEntries);

        if (parts.Length > 1 && !flags)
        {
            return false;
        }

        ulong bits = 0;

        foreach (var part in parts)
        {
            if (!TryParseName<TEnum>(part, out var member))
            {
                return false;
            }

            bits |= ToBits(member);
        }

        value = (TEnum)Enum.ToObject(typeof(TEnum), bits);

        return true;
    }

    private static bool IsFlags<TEnum>() where TEnum : struct, Enum
    {
        return typeof(TEnum).IsDefined(typeof(FlagsAttribute), false);
    }

    // Matches one member name ignoring case and underscores on both sides, so "game_master", "GameMaster" and
    // "gamemaster" agree, and so does "mountn_a" with a member named Mountn_a.
    private static bool TryParseName<TEnum>(string text, out TEnum value) where TEnum : struct, Enum
    {
        var key = text.Replace("_", string.Empty);

        foreach (var name in Enum.GetNames<TEnum>())
        {
            if (key.Length > 0 && string.Equals(name.Replace("_", string.Empty), key, StringComparison.OrdinalIgnoreCase))
            {
                value = Enum.Parse<TEnum>(name);

                return true;
            }
        }

        value = default;

        return false;
    }

    private static ulong ToBits<TEnum>(TEnum value) where TEnum : struct, Enum
    {
        return Type.GetTypeCode(Enum.GetUnderlyingType(typeof(TEnum))) switch
        {
            TypeCode.SByte or TypeCode.Int16 or TypeCode.Int32 or TypeCode.Int64 => unchecked((ulong)Convert.ToInt64(value)),
            _ => Convert.ToUInt64(value)
        };
    }
}
