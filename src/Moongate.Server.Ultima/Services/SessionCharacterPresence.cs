using Moongate.Core.Primitives;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     A character is in the world when a game session has it as its character.
/// </summary>
public sealed class SessionCharacterPresence : ICharacterPresence
{
    private readonly ISessionService _sessions;

    public SessionCharacterPresence(ISessionService sessions)
    {
        _sessions = sessions;
    }

    public bool IsInWorld(Serial characterId)
    {
        // Session values are read without the game loop; only writes need it.
        return _sessions.GetAll().Any(session => session.CharacterId == characterId);
    }
}
