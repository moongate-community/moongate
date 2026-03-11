using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Interfaces.Services.Accounting;

/// <summary>
/// Defines account management and authentication operations.
/// </summary>
public interface IAccountService
{
    /// <summary>
    /// Checks whether an account with the given username exists.
    /// </summary>
    /// <param name="username">Account username.</param>
    /// <returns><see langword="true" /> if the account exists; otherwise <see langword="false" />.</returns>
    Task<bool> CheckAccountExistsAsync(string username);

    /// <summary>
    /// Creates a new account with the provided credentials and account type.
    /// </summary>
    /// <param name="username">Account username.</param>
    /// <param name="password">Plain text password to hash and store.</param>
    /// <param name="email">Account e-mail address.</param>
    /// <param name="accountType">Account role/type.</param>
    Task<UOAccountEntity?> CreateAccountAsync(
        string username,
        string password,
        string email = "",
        AccountType accountType = AccountType.Regular
    );

    /// <summary>
    /// Deletes an account by identifier.
    /// </summary>
    /// <param name="accountId">Account serial identifier.</param>
    Task<bool> DeleteAccountAsync(Serial accountId);

    /// <summary>
    /// Gets an account by identifier.
    /// </summary>
    /// <param name="accountId">Account serial identifier.</param>
    /// <returns>The account when found; otherwise <see langword="null" />.</returns>
    Task<UOAccountEntity?> GetAccountAsync(Serial accountId);

    /// <summary>
    /// Gets all accounts.
    /// </summary>
    Task<IReadOnlyList<UOAccountEntity>> GetAccountsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to authenticate an account with username and password.
    /// </summary>
    /// <param name="username">Account username.</param>
    /// <param name="password">Plain text password.</param>
    /// <returns>The account when authentication succeeds; otherwise <see langword="null" />.</returns>
    Task<UOAccountEntity?> LoginAsync(string username, string password);

    /// <summary>
    /// Updates mutable account fields.
    /// </summary>
    Task<UOAccountEntity?> UpdateAccountAsync(
        Serial accountId,
        string? username = null,
        string? password = null,
        string? email = null,
        AccountType? accountType = null,
        bool? isLocked = null,
        bool clearRecoveryCode = false,
        CancellationToken cancellationToken = default
    );
}
