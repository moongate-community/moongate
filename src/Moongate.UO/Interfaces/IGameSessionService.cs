using Moongate.Core.Server.Interfaces.Services.Base;
using Moongate.UO.Data.Session;

namespace Moongate.UO.Interfaces;

public interface IGameSessionService : IMoongateService
{
    GameNetworkSession? GetSession(string sessionId, bool throwIfNotFound = true);

    IEnumerable<GameNetworkSession> GetSessions();

    IEnumerable<GameNetworkSession> QuerySessions(Func<GameNetworkSession, bool> predicate);
}
