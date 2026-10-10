namespace Moongate.Server.Ultima.Data.Spells;

/// <summary>
///     The root of <c>data/spells.toml</c>: one <c>[[spell]]</c> per spell of Magery.
/// </summary>
public class SpellsFile
{
    public List<SpellDefinition> Spell { get; set; } = [];
}
