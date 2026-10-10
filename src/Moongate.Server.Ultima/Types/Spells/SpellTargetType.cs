namespace Moongate.Server.Ultima.Types.Spells;

/// <summary>
///     What the target cursor of a spell asks the player to pick.
/// </summary>
public enum SpellTargetType
{
    /// <summary>
    ///     No target: the spell takes effect when the cast delay ends.
    /// </summary>
    None = 0,

    /// <summary>
    ///     A mobile, the caster included.
    /// </summary>
    Mobile = 1,

    /// <summary>
    ///     An item, such as a rune or a chest.
    /// </summary>
    Item = 2,

    /// <summary>
    ///     A place of the map; a click on a mobile or an item gives its place.
    /// </summary>
    Location = 3
}
