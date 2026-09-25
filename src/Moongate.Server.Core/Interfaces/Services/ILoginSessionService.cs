using System.Diagnostics.CodeAnalysis;
using Moongate.Network.Interfaces.Client;
using Moongate.Server.Core.Data.Sessions;

namespace Moongate.Server.Core.Interfaces.Services;

/// <summary>
///     Tracks login connections without creating game sessions.
/// </summary>
public interface ILoginSessionService
{
    /// <summary>
    ///     Gets the login session for a connection, replacing a disconnected session with the same ID.
    /// </summary>
    /// <param name="connection">
    ///     The network connection that owns the session.
    /// </param>
    /// <returns>
    ///     The current login session for the connection ID.
    /// </returns>
    LoginSession GetOrCreate(INetworkConnection connection);

    /// <summary>
    ///     Looks up a login session by network session ID.
    /// </summary>
    /// <param name="sessionId">
    ///     The network session ID.
    /// </param>
    /// <param name="session">
    ///     The matching session when found; otherwise <see langword="null" />.
    /// </param>
    /// <returns>
    ///     <see langword="true" /> when a session is registered for the ID.
    /// </returns>
    bool TryGet(long sessionId, [NotNullWhen(true)] out LoginSession? session);

    /// <summary>
    ///     Checks that a session still owns its ID and has a connected network client.
    /// </summary>
    /// <param name="session">
    ///     The session to check.
    /// </param>
    /// <returns>
    ///     <see langword="true" /> when the session is current and connected.
    /// </returns>
    bool IsCurrent(LoginSession session);

    /// <summary>
    ///     Removes and disconnects a session only if it is still the current one for its ID.
    /// </summary>
    /// <param name="session">
    ///     The session to remove.
    /// </param>
    /// <returns>
    ///     <see langword="true" /> when that exact session was removed.
    /// </returns>
    bool Remove(LoginSession session);
}
