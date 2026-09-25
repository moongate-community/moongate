using System.Diagnostics.CodeAnalysis;
using Moongate.Network.Interfaces.Client;
using Moongate.Server.Core.Data.Sessions;

namespace Moongate.Server.Core.Interfaces.Services;

/// <summary>
///     Tracks login connections without creating game sessions.
/// </summary>
public interface ILoginSessionService
{
    LoginSession GetOrCreate(INetworkConnection connection);

    bool TryGet(long sessionId, [NotNullWhen(true)] out LoginSession? session);

    bool IsCurrent(LoginSession session);

    bool Remove(LoginSession session);
}
