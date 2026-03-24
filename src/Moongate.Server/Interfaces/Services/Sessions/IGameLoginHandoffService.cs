using Moongate.Server.Data.Session;
using Moongate.UO.Data.Version;

namespace Moongate.Server.Interfaces.Services.Sessions;

/// <summary>
/// Stores one-shot client handshake metadata across the login-server to game-server redirect.
/// </summary>
public interface IGameLoginHandoffService
{
    /// <summary>
    /// Stores client metadata for a redirect session key.
    /// </summary>
    /// <param name="sessionKey">Redirect session key.</param>
    /// <param name="clientType">Resolved client type from the login socket.</param>
    /// <param name="clientVersion">Negotiated client version from the login socket, when known.</param>
    void Store(uint sessionKey, ClientType clientType, ClientVersion? clientVersion);

    /// <summary>
    /// Tries to consume a previously stored redirect handoff.
    /// </summary>
    /// <param name="sessionKey">Redirect session key.</param>
    /// <param name="handoff">Resolved handoff metadata.</param>
    /// <returns><c>true</c> when a matching handoff existed and was consumed.</returns>
    bool TryConsume(uint sessionKey, out GameLoginHandoff handoff);
}
