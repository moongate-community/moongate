namespace Moongate.Server.Ultima.Data.Internal.Movement;

/// <summary>
///     The steps of one session: the sequence the next step must carry and when it may come. Changed on the game loop.
/// </summary>
public sealed class MovementState
{
    /// <summary>
    ///     Gets or sets the sequence the next step must carry: 0 after login or a refused step, then 1 to 255.
    /// </summary>
    public byte ExpectedSequence { get; set; }

    /// <summary>
    ///     Gets or sets when the next step is due, in milliseconds of the server clock.
    /// </summary>
    public long NextStepAt { get; set; }
}
