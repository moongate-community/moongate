using Moongate.Core.Server.Interfaces.Services.Base;
using Moongate.UO.Data.Session;

namespace Moongate.UO.Interfaces.Services;

public interface IGameSessionService : IMoongateService
{

    delegate void GameSessionCreatedHandler(GameNetworkSession session);
    delegate void GameSessionDestroyedHandler(GameNetworkSession session);
    delegate void GameSessionBeforeDestroyHandler(GameNetworkSession session);

    event GameSessionCreatedHandler GameSessionCreated;
    event GameSessionDestroyedHandler GameSessionDestroyed;
    event GameSessionBeforeDestroyHandler GameSessionBeforeDestroy;

    GameNetworkSession? GetSession(string sessionId, bool throwIfNotFound = true);

    IEnumerable<GameNetworkSession> GetSessions();

    IEnumerable<GameNetworkSession> QuerySessions(Func<GameNetworkSession, bool> predicate);
}
