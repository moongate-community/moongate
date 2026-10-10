namespace Moongate.Server.Ultima.Types.Spells;

/// <summary>
///     What came of dropping an item on a spellbook.
/// </summary>
public enum SpellbookDropType
{
    /// <summary>
    ///     The item is no scroll of a spell, or the book is not the player's: nothing was done.
    /// </summary>
    Ignored = 0,

    /// <summary>
    ///     The book held the spell already and the player was told so.
    /// </summary>
    AlreadyPresent = 1,

    /// <summary>
    ///     The spell was added and one scroll used up.
    /// </summary>
    Added = 2
}
