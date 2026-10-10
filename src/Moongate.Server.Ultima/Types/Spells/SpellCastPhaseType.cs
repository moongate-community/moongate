namespace Moongate.Server.Ultima.Types.Spells;

/// <summary>
///     Where a cast of a spell is: the words are said and the delay runs, or it waits for the player to pick a target.
/// </summary>
public enum SpellCastPhaseType
{
    Casting = 0,
    Targeting = 1
}
