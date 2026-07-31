using Moongate.Core.Primitives;

namespace Moongate.Server.Abstractions.Data.World;

/// <summary>
/// What changed in one session's view: what it must now be shown, and what it must be told is gone.
/// </summary>
/// <param name="Entered">Serials the client did not know and now does.</param>
/// <param name="Left">Serials the client knew and must forget.</param>
public readonly record struct VisibilityDelta(IReadOnlyList<Serial> Entered, IReadOnlyList<Serial> Left)
{
    /// <summary>Nothing moved in or out — the common case, and the one that must send no packets.</summary>
    public bool IsEmpty
        => Entered.Count == 0 && Left.Count == 0;
}
