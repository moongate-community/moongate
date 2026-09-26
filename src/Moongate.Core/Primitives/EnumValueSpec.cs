using Moongate.Core.Random;
using Moongate.Core.Utils;

namespace Moongate.Core.Primitives;

/// <summary>
///     An enum field that is either a fixed value or a policy for picking one at random each time
///     <see cref="Resolve" /> is called, such as when a template spawns an entity.
/// </summary>
/// <remarks>
///     Written as text: a bare member name (
///     <c>
///         common
///     </c>
///     ) is fixed;
///     <c>
///         random_of
///     </c>
///     picks among every
///     member of <typeparamref name="TEnum" />;
///     <c>
///         random_of:rare,epic,legendary
///     </c>
///     picks among only the
///     named members. Parsing is case-insensitive; <see cref="ToString" /> writes lowercase.
/// </remarks>
public readonly struct EnumValueSpec<TEnum> where TEnum : struct, Enum
{
    private const string RandomOfPrefix = "random_of";

    private readonly TEnum[]? _candidates;
    private readonly TEnum _fixedValue;

    /// <summary>
    ///     Gets whether this resolves to a random pick rather than always the same value.
    /// </summary>
    public bool IsRandom => _candidates is not null;

    private EnumValueSpec(TEnum fixedValue)
    {
        _fixedValue = fixedValue;
        _candidates = null;
    }

    private EnumValueSpec(TEnum[] candidates)
    {
        _fixedValue = default;
        _candidates = candidates;
    }

    /// <summary>
    ///     Creates a spec that always resolves to <paramref name="value" />.
    /// </summary>
    public static EnumValueSpec<TEnum> FromValue(TEnum value)
    {
        return new(value);
    }

    /// <summary>
    ///     Creates a spec that resolves to a random pick among <paramref name="candidates" />.
    /// </summary>
    public static EnumValueSpec<TEnum> FromCandidates(IReadOnlyList<TEnum> candidates)
    {
        if (candidates.Count == 0)
        {
            throw new ArgumentException("At least one candidate is required.", nameof(candidates));
        }

        return new([.. candidates]);
    }

    /// <summary>
    ///     Creates a spec that resolves to a random pick among every member of <typeparamref name="TEnum" />.
    /// </summary>
    public static EnumValueSpec<TEnum> Random()
    {
        return new(Enum.GetValues<TEnum>());
    }

    /// <summary>
    ///     Parses the text a template writer would use: a bare member name,
    ///     <c>
    ///         random_of
    ///     </c>
    ///     , or
    ///     <c>
    ///         random_of:member,member,...
    ///     </c>
    ///     .
    /// </summary>
    /// <returns>
    ///     False, with <paramref name="spec" /> left default, when the text is not valid.
    /// </returns>
    public static bool TryParse(string? text, out EnumValueSpec<TEnum> spec)
    {
        spec = default;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmed = text.Trim();

        if (trimmed.Equals(RandomOfPrefix, StringComparison.OrdinalIgnoreCase))
        {
            spec = Random();

            return true;
        }

        var prefix = RandomOfPrefix + ":";

        if (trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            var names = trimmed[prefix.Length..]
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            if (names.Length == 0)
            {
                return false;
            }

            var candidates = new TEnum[names.Length];

            for (var i = 0; i < names.Length; i++)
            {
                if (!EnumNameUtils.TryParse<TEnum>(names[i], out var candidate))
                {
                    return false;
                }

                candidates[i] = candidate;
            }

            spec = FromCandidates(candidates);

            return true;
        }

        if (!EnumNameUtils.TryParse<TEnum>(trimmed, out var value))
        {
            return false;
        }

        spec = FromValue(value);

        return true;
    }

    /// <summary>
    ///     Resolves the value: the fixed value, or a fresh random pick among the candidates.
    /// </summary>
    public TEnum Resolve()
    {
        return IsRandom ? _candidates![BuiltInRng.Next(_candidates.Length)] : _fixedValue;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return IsRandom
            ? $"{RandomOfPrefix}:{string.Join(',', _candidates!.Select(EnumNameUtils.Format))}"
            : EnumNameUtils.Format(_fixedValue);
    }
}
