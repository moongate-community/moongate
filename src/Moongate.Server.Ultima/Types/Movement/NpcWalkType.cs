namespace Moongate.Server.Ultima.Types.Movement;

/// <summary>
///     What an NPC walking to a place does on a tick.
/// </summary>
public enum NpcWalkType : byte
{
    /// <summary>
    ///     It stands at the place, or within the range asked of it.
    /// </summary>
    Arrived,

    /// <summary>
    ///     It has a step to take towards the place.
    /// </summary>
    Moving,

    /// <summary>
    ///     Its way was blocked, or the place moved, and it is too soon to look for another way.
    /// </summary>
    Blocked,

    /// <summary>
    ///     No way to the place was found; it is looked for again later.
    /// </summary>
    NoPath
}
