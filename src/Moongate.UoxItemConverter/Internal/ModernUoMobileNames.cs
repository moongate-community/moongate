using Moongate.Core.Utils;

namespace Moongate.UoxItemConverter.Internal;

/// <summary>
///     Finds the Moongate mobile template of a ModernUO mobile class: an alias of this table, else the class in snake
///     case (GreatHart is great_hart), else the class with the underscores of the ids ignored, else UOX3's short name of
///     an elemental (DullCopperElemental is dullcopperele).
/// </summary>
internal static class ModernUoMobileNames
{
    private const string ElementalSuffix = "Elemental";

    // Classes whose template has another name, or the nearest one of the same kind.
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Armorer"] = "armourer",
        ["Barkeeper"] = "tavernkeeper",
        ["BoneDemon"] = "bonedaemon",
        ["BoneMagi"] = "bonemage",
        ["CustomHairstylist"] = "hairstylist",
        ["GenericGuard"] = "guard",
        ["GreatHart"] = "hart",
        ["GrizzlyBear"] = "grizbear",
        ["HeadlessOne"] = "headless",
        ["HireBeggar"] = "beggar",
        ["Minter"] = "banker",
        ["OrcishLord"] = "orclord",
        ["OrcishMage"] = "orcmage",
        ["RidableLlama"] = "llama",
        ["WanderingHealer"] = "healer"
    };

    /// <summary>
    ///     Gets the template id of <paramref name="className" /> among <paramref name="flatIds" />, ids keyed by their
    ///     lowercase form without underscores; null when there is none.
    /// </summary>
    public static string? Resolve(string className, IReadOnlyDictionary<string, string> flatIds, IReadOnlySet<string> ids)
    {
        if (Aliases.TryGetValue(className, out var alias) && ids.Contains(alias))
        {
            return alias;
        }

        var snake = StringUtils.ToSnakeCase(className);

        if (ids.Contains(snake))
        {
            return snake;
        }

        if (flatIds.TryGetValue(Flat(className), out var flat))
        {
            return flat;
        }

        return className.EndsWith(ElementalSuffix, StringComparison.OrdinalIgnoreCase) &&
               flatIds.TryGetValue(Flat(className[..^ElementalSuffix.Length]) + "ele", out var elemental)
            ? elemental
            : null;
    }

    /// <summary>
    ///     Gets the lowercase form of <paramref name="name" /> without underscores, the key of the flattened ids.
    /// </summary>
    public static string Flat(string name)
    {
        return name.Replace("_", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
    }
}
