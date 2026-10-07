namespace Moongate.Server.Ultima.Types.Movement;

/// <summary>
///     What a mover can move over when <c>IMovementService</c> checks a step.
/// </summary>
[Flags]
public enum MovementAbilityType : byte
{
    /// <summary>
    ///     Moves over nothing.
    /// </summary>
    None = 0,

    /// <summary>
    ///     Land, statics and surfaces that are not water.
    /// </summary>
    Walk = 1,

    /// <summary>
    ///     Water tiles, flagged <c>Wet</c>.
    /// </summary>
    Swim = 2,

    /// <summary>
    ///     Doors lying on the ground do not block, as for ModernUO's staff body and ghosts.
    /// </summary>
    PassDoors = 4
}
