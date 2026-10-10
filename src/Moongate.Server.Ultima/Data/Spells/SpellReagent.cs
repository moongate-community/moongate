namespace Moongate.Server.Ultima.Data.Spells;

/// <summary>
///     One reagent of a spell in <c>data/spells.toml</c>: the item template and how many a cast takes.
/// </summary>
public class SpellReagent
{
    /// <summary>
    ///     The id of the item template of the reagent, such as <c>0x0f7a_black_pearl</c>.
    /// </summary>
    public string Template { get; set; } = string.Empty;

    /// <summary>
    ///     How many of it a cast takes from the backpack. 1 or more.
    /// </summary>
    public int Amount { get; set; } = 1;
}
