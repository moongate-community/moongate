namespace Moongate.Server.Ultima.Data.Internal.HuePicking;

/// <summary>
///     The hue picker of one session: the last id handed out and the picker still waiting for an answer. Changed on
///     the game loop.
/// </summary>
public sealed class HuePickerState
{
    /// <summary>
    ///     Gets or sets the id of the last picker sent; the next one takes the following id.
    /// </summary>
    public int LastId { get; set; }

    /// <summary>
    ///     Gets or sets the picker waiting for the player's answer, if any.
    /// </summary>
    public PendingHuePicker? Pending { get; set; }
}
