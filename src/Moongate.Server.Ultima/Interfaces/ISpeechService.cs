using Moongate.Server.Ultima.Entities.World;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     Makes a mobile speak overhead to the players around it. Called on the game loop.
/// </summary>
public interface ISpeechService
{
    /// <summary>
    ///     Sends <paramref name="text" /> as regular speech of <paramref name="speaker" /> (0xAE) to the players within 15
    ///     cells on its map.
    /// </summary>
    /// <returns>How many players it was sent to.</returns>
    int Say(MobileEntity speaker, string text);
}
