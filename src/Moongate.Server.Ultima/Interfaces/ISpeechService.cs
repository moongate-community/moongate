using Moongate.Server.Ultima.Entities.World;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     What the players around a mobile hear: its overhead speech and the sounds it makes. Called on the game loop.
/// </summary>
public interface ISpeechService
{
    /// <summary>
    ///     Sends <paramref name="text" /> as regular speech of <paramref name="speaker" /> (0xAE) to the players within 15
    ///     cells on its map.
    /// </summary>
    /// <returns>How many players it was sent to.</returns>
    int Say(MobileEntity speaker, string text);

    /// <summary>
    ///     Plays <paramref name="sound" /> once where <paramref name="source" /> stands (0x54), for the players within 15
    ///     cells on its map.
    /// </summary>
    /// <returns>How many players it was sent to.</returns>
    int PlaySound(MobileEntity source, int sound);
}
