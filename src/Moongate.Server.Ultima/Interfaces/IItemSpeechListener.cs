using Moongate.Server.Ultima.Entities.World;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     Tells the items around a player what the player said. Called on the game loop by the speech handler.
/// </summary>
public interface IItemSpeechListener
{
    /// <summary>
    ///     <paramref name="speaker" /> said <paramref name="text" /> aloud, with the speech keywords the client found in
    ///     it; commands never reach it.
    /// </summary>
    void Heard(MobileEntity speaker, string text, IReadOnlyList<int>? keywords = null);
}
