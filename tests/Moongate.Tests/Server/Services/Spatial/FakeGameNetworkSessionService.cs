using Moongate.Network.Client;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.UO.Data.Ids;

namespace Moongate.Tests.Server.Services.Spatial;

public sealed class FakeGameNetworkSessionService : IGameNetworkSessionService
{
    private readonly List<GameSession> _sessions = [];

    public int Count => _sessions.Count;

    public void Add(GameSession session)
        => _sessions.Add(session);

    public void Clear()
        => _sessions.Clear();

    public IReadOnlyCollection<GameSession> GetAll()
        => _sessions.ToArray();

    public GameSession GetOrCreate(MoongateTCPClient client)
        => throw new NotSupportedException();

    public bool Remove(long sessionId)
    {
        var removed = _sessions.RemoveAll(session => session.SessionId == sessionId);
        return removed > 0;
    }

    public bool TryGet(long sessionId, out GameSession session)
    {
        session = _sessions.FirstOrDefault(item => item.SessionId == sessionId)!;
        return session is not null;
    }

    public bool TryGetByCharacterId(Serial characterId, out GameSession session)
    {
        session = _sessions.FirstOrDefault(item => item.CharacterId == characterId)!;
        return session is not null;
    }
}
