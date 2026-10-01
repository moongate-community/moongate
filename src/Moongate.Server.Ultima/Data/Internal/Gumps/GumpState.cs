namespace Moongate.Server.Ultima.Data.Internal.Gumps;

/// <summary>
///     The gumps open on one session, oldest first. Changed on the game loop.
/// </summary>
public sealed class GumpState
{
    public List<OpenGump> Open { get; } = [];

    /// <summary>
    ///     Gets or sets whether the session is closing: nothing more opens on it.
    /// </summary>
    public bool Closing { get; set; }
}
