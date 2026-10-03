namespace Moongate.Server.Ultima.Data.Internal.Prompts;

/// <summary>
///     The text prompt of one session: the last id handed out and the prompt still waiting for an answer. Changed on
///     the game loop.
/// </summary>
public sealed class PromptState
{
    /// <summary>
    ///     Gets or sets the id of the last prompt sent; the next one takes the following id.
    /// </summary>
    public int LastId { get; set; }

    /// <summary>
    ///     Gets or sets the prompt waiting for the player's answer, if any.
    /// </summary>
    public PendingPrompt? Pending { get; set; }
}
