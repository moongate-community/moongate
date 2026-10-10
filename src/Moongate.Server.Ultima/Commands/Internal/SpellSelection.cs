using System.Globalization;
using Moongate.Server.Ultima.Data.Spells;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Server.Ultima.Commands.Internal;

/// <summary>
///     The spells a staff command names: one by key or number, <c>circle N</c>, or <c>all</c>. It reads them from the
///     start of the arguments and says how many arguments it used, so a command may take more after them.
/// </summary>
internal static class SpellSelection
{
    public const int FirstCircle = 1;
    public const int LastCircle = 8;

    /// <summary>
    ///     What went wrong when the arguments name no spells.
    /// </summary>
    public enum Failure
    {
        None,
        Usage,
        UnknownSpell,
        UnknownCircle
    }

    /// <summary>
    ///     Reads the spells from the arguments. <paramref name="detail" /> is the word that was not understood, for
    ///     the two unknown failures.
    /// </summary>
    public static Failure TryRead(
        ISpellCatalogService catalog,
        string[] arguments,
        out IReadOnlyList<SpellDefinition> spells,
        out int used,
        out string detail
    )
    {
        spells = [];
        used = 0;
        detail = "";

        if (arguments.Length == 0)
        {
            return Failure.Usage;
        }

        var word = arguments[0];

        if (word.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            spells = catalog.All;
            used = 1;

            return Failure.None;
        }

        if (word.Equals("circle", StringComparison.OrdinalIgnoreCase))
        {
            if (arguments.Length < 2)
            {
                return Failure.Usage;
            }

            detail = arguments[1];

            if (!int.TryParse(detail, NumberStyles.None, CultureInfo.InvariantCulture, out var circle) ||
                circle is < FirstCircle or > LastCircle)
            {
                return Failure.UnknownCircle;
            }

            spells = catalog.All.Where(spell => spell.Circle == circle).ToList();
            used = 2;

            return Failure.None;
        }

        detail = word;
        SpellDefinition? found;

        var known = int.TryParse(word, NumberStyles.None, CultureInfo.InvariantCulture, out var number)
            ? catalog.TryGet(number, out found)
            : catalog.TryGetByKey(word.ToLowerInvariant(), out found);

        if (!known || found is null)
        {
            return Failure.UnknownSpell;
        }

        spells = [found];
        used = 1;

        return Failure.None;
    }
}
