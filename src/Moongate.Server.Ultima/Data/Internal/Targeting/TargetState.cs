namespace Moongate.Server.Ultima.Data.Internal.Targeting;

/// <summary>
///     The target cursor of one session: the last id handed out and the target still waiting for an answer. Changed on
///     the game loop.
/// </summary>
public sealed class TargetState
{
    /// <summary>
    ///     Gets or sets the id of the last cursor sent; the next one takes the following id.
    /// </summary>
    public int LastId { get; set; }

    /// <summary>
    ///     Gets or sets the target waiting for the player's answer, if any.
    /// </summary>
    public PendingTarget? Pending { get; set; }
}
