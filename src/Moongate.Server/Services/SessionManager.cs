using System.Collections.Concurrent;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces;
using SquidStd.Network.Client;

namespace Moongate.Server.Services;

/// <summary>In-memory session registry keyed by <see cref="SquidStdTcpClient.SessionId" />.</summary>
public sealed class SessionManager : ISessionManager
{
    private readonly ConcurrentDictionary<long, PlayerSession> _sessions = new();

    public int Count => _sessions.Count;

    public PlayerSession GetOrCreate(SquidStdTcpClient client)
    {
        return _sessions.GetOrAdd(client.SessionId, _ => new PlayerSession(client));
    }

    public bool TryGet(long sessionId, out PlayerSession session)
    {
        return _sessions.TryGetValue(sessionId, out session!);
    }

    public void Remove(long sessionId)
    {
        _sessions.TryRemove(sessionId, out _);
    }
}
