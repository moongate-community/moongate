using Moongate.Core.Primitives;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     Tells whether a character is being played right now.
/// </summary>
public interface ICharacterPresence
{
    /// <summary>
    ///     Gets whether a game session is playing <paramref name="characterId" />.
    /// </summary>
    bool IsInWorld(Serial characterId);
}
