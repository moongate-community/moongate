namespace Moongate.Server.Ultima.Types.Movement;

/// <summary>
///     How a search for a path ended.
/// </summary>
public enum PathResultType : byte
{
    /// <summary>
    ///     The steps lead to the goal.
    /// </summary>
    Found,

    /// <summary>
    ///     The goal cannot be reached; the steps lead to the closest place found. Only when a partial path was asked for.
    /// </summary>
    Partial,

    /// <summary>
    ///     No path within the limits of the search: the goal is walled in, too many places were looked at, or the map is
    ///     not loaded.
    /// </summary>
    NotFound,

    /// <summary>
    ///     Start and goal are farther apart than the search looks; nothing was searched.
    /// </summary>
    TooFar
}
