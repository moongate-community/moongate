using Moongate.Network.Client;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Services.Sessions;

namespace Moongate.Tests.Server.Support;

public sealed class SpeechServiceTestGameNetworkSessionService : IGameNetworkSessionService
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
        => throw new NotSupportedException();

    public bool TryGet(long sessionId, out GameSession session)
        => throw new NotSupportedException();
}
