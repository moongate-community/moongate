using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Data.Account;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     Account authority coordinating SQL security changes and shared administrative sessions.
/// </summary>
/// <remarks>
///     Callers authorize mutations. Direct SQL or generic entity writes bypass this revocation guarantee.
/// </remarks>
public interface IAccountAdminAccessService
{
    /// <summary>
    ///     Authenticates an unlocked API-enabled account and issues a token after SQL commit.
    /// </summary>
    Task<AdminLoginResult?> LoginAsync(string username, string password, CancellationToken token = default);

    /// <summary>
    ///     Changes API access and revokes prior sessions. Missing accounts throw KeyNotFoundException.
    /// </summary>
    Task SetApiAccessAsync(string username, bool enabled, CancellationToken token = default);

    /// <summary>
    ///     Changes lock, role and API access while revoking prior sessions.
    /// </summary>
    Task UpdateAccessAsync(Serial accountId, AccountAccessOptions options, CancellationToken token = default);

    /// <summary>
    ///     Changes a password while revoking prior administrative sessions.
    /// </summary>
    Task ChangePasswordAsync(Serial accountId, string password, CancellationToken token = default);

    /// <summary>
    ///     Revokes every administrative session without changing account security fields.
    /// </summary>
    Task RevokeSessionsAsync(Serial accountId, CancellationToken token = default);
}
