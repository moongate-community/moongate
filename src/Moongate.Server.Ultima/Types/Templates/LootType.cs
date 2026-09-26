namespace Moongate.Server.Ultima.Types.Templates;

/// <summary>
///     What happens to an item when its owner dies, as in ModernUO.
/// </summary>
public enum LootType : byte
{
    /// <summary>
    ///     Dropped on the corpse like any item.
    /// </summary>
    Regular = 0,

    /// <summary>
    ///     Kept by a new player's character.
    /// </summary>
    Newbied = 1,

    /// <summary>
    ///     Always kept by its owner.
    /// </summary>
    Blessed = 2,

    /// <summary>
    ///     Always dropped.
    /// </summary>
    Cursed = 3
}
