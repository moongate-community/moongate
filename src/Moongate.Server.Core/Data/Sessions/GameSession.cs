using Moongate.Core.Primitives;
using Moongate.Server.Core.Interfaces.Services;

namespace Moongate.Server.Core.Data.Sessions;

public sealed class GameSession
{
    private readonly IGameLoopService _gameLoop;
    private readonly Lock _sync = new();

    private Serial _accountId;
    private Serial _characterId;

    public NetworkSession NetworkSession { get; }

    public long SessionId { get; }

    public Serial AccountId
    {
        get
        {
            lock (_sync)
            {
                return _accountId;
            }
        }
    }

    public Serial CharacterId
    {
        get
        {
            lock (_sync)
            {
                return _characterId;
            }
        }
    }

    public GameSession(NetworkSession networkSession, IGameLoopService gameLoop)
    {
        ArgumentNullException.ThrowIfNull(networkSession);
        ArgumentNullException.ThrowIfNull(gameLoop);
        NetworkSession = networkSession;
        SessionId = networkSession.SessionId;
        _gameLoop = gameLoop;
    }

    public void SetAccountId(Serial accountId)
    {
        EnsureLoopThread();
        lock (_sync)
        {
            _accountId = accountId;
        }
    }

    public void SetCharacterId(Serial characterId)
    {
        EnsureLoopThread();
        lock (_sync)
        {
            _characterId = characterId;
        }
    }

    private void EnsureLoopThread()
    {
        if (!_gameLoop.IsOnLoopThread)
        {
            throw new InvalidOperationException("Session game state must be changed on the game loop thread.");
        }
    }
}
