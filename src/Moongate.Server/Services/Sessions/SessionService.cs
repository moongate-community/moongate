using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Moongate.Core.Primitives;
using Moongate.Network.Interfaces.Client;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Services;

namespace Moongate.Server.Services.Sessions;

public sealed class SessionService : ISessionService
{
    private readonly ConcurrentDictionary<long, GameSession> _sessions = new();
    private readonly IGameLoopService _gameLoop;

    public int Count => _sessions.Count;

    public SessionService(IGameLoopService gameLoop)
    {
        _gameLoop = gameLoop;
    }

    public void Clear()
    {
        _sessions.Clear();
    }

    public IReadOnlyCollection<GameSession> GetAll()
    {
        return _sessions.Values.ToArray();
    }

    public GameSession GetOrCreate(INetworkConnection client)
    {
        ArgumentNullException.ThrowIfNull(client);

        return _sessions.GetOrAdd(
            client.SessionId,
            _ => new(new(client), _gameLoop)
        );
    }

    public bool Remove(long sessionId)
    {
        return _sessions.TryRemove(sessionId, out _);
    }

    public bool TryGet(long sessionId, [NotNullWhen(true)] out GameSession? session)
    {
        return _sessions.TryGetValue(sessionId, out session);
    }

    public bool TryGetByCharacterId(Serial characterId, [NotNullWhen(true)] out GameSession? session)
    {
        session = null;

        if (characterId == Serial.Zero)
        {
            return false;
        }

        foreach (var candidate in _sessions.Values)
        {
            if (candidate.CharacterId == characterId)
            {
                session = candidate;

                return true;
            }
        }

        return false;
    }
}
