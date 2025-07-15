using Moongate.Core.Server.Interfaces.Services.Base;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Session;

namespace Moongate.UO.Interfaces.Services;

public interface IGameSessionService : IMoongateService
{

    delegate void GameSessionCreatedHandler(GameSession session);
    delegate void GameSessionDestroyedHandler(GameSession session);
    delegate void GameSessionBeforeDestroyHandler(GameSession session);

    event GameSessionCreatedHandler GameSessionCreated;
    event GameSessionDestroyedHandler GameSessionDestroyed;
    event GameSessionBeforeDestroyHandler GameSessionBeforeDestroy;

    GameSession? GetSession(string sessionId, bool throwIfNotFound = true);

    GameSession? GetGameSessionByMobile(UOMobileEntity mobile, bool throwIfNotFound = true);

    IEnumerable<GameSession> GetSessions();

    IEnumerable<GameSession> QuerySessions(Func<GameSession, bool> predicate);

    GameSession? QuerySessionFirstOrDefault(Func<GameSession, bool> predicate);
}
