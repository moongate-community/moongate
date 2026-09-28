using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Interfaces.Sessions;

namespace Moongate.Tests.TestSupport.Sessions;

/// <summary>
///     Records the sessions it is told about: their character, whether they were still registered and whether the
///     call came on the game loop.
/// </summary>
public sealed class RecordingSessionClosedListener : ISessionClosedListener
{
    private readonly IGameLoopService _gameLoop;
    private readonly ISessionService _sessions;

    public List<(long SessionId, Serial CharacterId, bool StillRegistered, bool OnLoop)> Closed { get; } = [];

    public Exception? Throw { get; set; }

    public RecordingSessionClosedListener(IGameLoopService gameLoop, ISessionService sessions)
    {
        _gameLoop = gameLoop;
        _sessions = sessions;
    }

    public void OnSessionClosed(GameSession session)
    {
        Closed.Add((session.SessionId, session.CharacterId, _sessions.TryGet(session.SessionId, out _), _gameLoop.IsOnLoopThread));

        if (Throw is not null)
        {
            throw Throw;
        }
    }
}
