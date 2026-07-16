using System.Collections.Concurrent;
using Moongate.Core.Primitives;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Accounts;
using SquidStd.Network.Client;

namespace Moongate.Server.Services.Accounts;

/// <summary>In-memory session registry keyed by <see cref="SquidStdTcpClient.SessionId" />.</summary>
public sealed class SessionManager : ISessionManager
{
    private readonly ConcurrentDictionary<long, PlayerSession> _sessions = new();

    public int Count => _sessions.Count;

    public PlayerSession GetOrCreate(SquidStdTcpClient client)
        => _sessions.GetOrAdd(client.SessionId, _ => new(client));

    public bool IsCharacterPlayed(Serial mobileId)
        => _sessions.Values.Any(session => session.Character?.Id == mobileId);

    public void Remove(long sessionId)
        => _sessions.TryRemove(sessionId, out _);

    public bool TryGet(long sessionId, out PlayerSession session)
        => _sessions.TryGetValue(sessionId, out session!);
}
