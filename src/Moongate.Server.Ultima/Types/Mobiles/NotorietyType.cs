namespace Moongate.Server.Ultima.Types.Mobiles;

/// <summary>
///     The name colour the client shows for a mobile; the values are the client's.
/// </summary>
public enum NotorietyType : byte
{
    /// <summary>
    ///     Blue: attacking it is a crime.
    /// </summary>
    Innocent = 1,

    /// <summary>
    ///     Green: a friend, such as a guild member.
    /// </summary>
    Ally = 2,

    /// <summary>
    ///     Grey: anyone may attack it.
    /// </summary>
    Attackable = 3,

    /// <summary>
    ///     Grey: a criminal.
    /// </summary>
    Criminal = 4,

    /// <summary>
    ///     Orange: an enemy, such as a member of a guild at war.
    /// </summary>
    Enemy = 5,

    /// <summary>
    ///     Red: a murderer or a hostile monster.
    /// </summary>
    Murderer = 6,

    /// <summary>
    ///     Yellow: cannot be attacked, such as a vendor or a guard.
    /// </summary>
    Invulnerable = 7
}
