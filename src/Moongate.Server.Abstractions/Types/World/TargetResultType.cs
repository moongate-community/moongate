namespace Moongate.Server.Abstractions.Types.World;

/// <summary>What the player did with a target cursor.</summary>
public enum TargetResultType : byte
{
    /// <summary>
    /// The player dismissed the cursor, or the request was superseded by another. The common case:
    /// a player presses Escape constantly, and a caller that ignores this acts on a serial of zero.
    /// </summary>
    Cancelled = 0,

    /// <summary>An entity was clicked.</summary>
    Object,

    /// <summary>A spot on the ground was clicked.</summary>
    Location
}
