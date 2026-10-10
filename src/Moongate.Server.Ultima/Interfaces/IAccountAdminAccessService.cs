using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Data.Account;
using Moongate.Server.Ultima.Entities.Auth;

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
    ///     Changes only the settings set in <paramref name="patch" />, inside the same transaction, and revokes prior sessions.
    ///     Gives the account as it is after the change. Missing accounts throw KeyNotFoundException.
    /// </summary>
    Task<AccountEntity> PatchAccessAsync(Serial accountId, AccountAccessPatch patch, CancellationToken token = default);

    /// <summary>
    ///     Changes the password of an account given its current one, revoking prior sessions. A wrong current password throws
    ///     <see cref="WrongCurrentPasswordException" />; the sessions stay.
    /// </summary>
    Task ChangeOwnPasswordAsync(
        Serial accountId,
        string currentPassword,
        string newPassword,
        CancellationToken token = default
    );

    /// <summary>
    ///     Changes a password while revoking prior administrative sessions.
    /// </summary>
    Task ChangePasswordAsync(Serial accountId, string password, CancellationToken token = default);

    /// <summary>
    ///     Revokes every administrative session without changing account security fields.
    /// </summary>
    Task RevokeSessionsAsync(Serial accountId, CancellationToken token = default);
}
