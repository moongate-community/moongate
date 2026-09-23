using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Moongate.Network.Interfaces.Client;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Services;

namespace Moongate.Server.Services.Login;

public sealed class LoginSessionService : ILoginSessionService
{
    private readonly ConcurrentDictionary<long, LoginSession> _sessions = new();

    public LoginSession GetOrCreate(INetworkConnection connection)
        => _sessions.AddOrUpdate(connection.SessionId,
            _ => new(connection),
            (_, current) => current.IsDisconnected ? new(connection) : current);

    public bool TryGet(long sessionId, [NotNullWhen(true)] out LoginSession? session)
        => _sessions.TryGetValue(sessionId, out session);

    public bool IsCurrent(LoginSession session)
        => _sessions.TryGetValue(session.SessionId, out var current) &&
           ReferenceEquals(current, session) && !session.IsDisconnected &&
           session.NetworkSession.Client is { IsConnected: true };

    public bool Remove(LoginSession session)
    {
        if (!_sessions.TryRemove(new KeyValuePair<long, LoginSession>(session.SessionId, session)))
        {
            return false;
        }

        session.Disconnect();
        return true;
    }
}
