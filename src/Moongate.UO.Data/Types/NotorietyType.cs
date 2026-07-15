namespace Moongate.UO.Data.Types;

/// <summary>
/// A mobile's notoriety as sent on the wire (the highlight colour the client draws): innocent blue,
/// ally green, attackable/criminal grey, enemy orange, murderer red, or invulnerable yellow.
/// </summary>
public enum NotorietyType : byte
{
    None = 0,
    Innocent = 1,
    Ally = 2,
    CanBeAttacked = 3,
    Criminal = 4,
    Enemy = 5,
    Murderer = 6,
    Invulnerable = 7
}
