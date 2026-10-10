using System.Diagnostics.CodeAnalysis;
using Moongate.Server.Ultima.Data.Spells;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     The spells of <c>data/spells.toml</c>, by their number, their key and the graphic of their scroll.
/// </summary>
public interface ISpellCatalogService
{
    /// <summary>
    ///     Gets every spell, in the order of the client's spellbook.
    /// </summary>
    IReadOnlyList<SpellDefinition> All { get; }

    /// <summary>
    ///     Gets the spell with a client number (1 to 64); false for a number that is none.
    /// </summary>
    bool TryGet(int id, [NotNullWhen(true)] out SpellDefinition? spell);

    /// <summary>
    ///     Gets the spell with a key, such as <c>magic_arrow</c>; false for an unknown key.
    /// </summary>
    bool TryGetByKey(string key, [NotNullWhen(true)] out SpellDefinition? spell);

    /// <summary>
    ///     Gets the spell a scroll item holds, by the graphic of the item: every scroll template of a spell, the aliases
    ///     too, has the graphic of its canonical one. False for a graphic that is no scroll.
    /// </summary>
    bool TryGetByScrollGraphic(int graphic, [NotNullWhen(true)] out SpellDefinition? spell);
}
