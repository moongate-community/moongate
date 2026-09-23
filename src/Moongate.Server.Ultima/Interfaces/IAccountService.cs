using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Ultima.Data.Account;
using Moongate.Server.Ultima.Entities.Auth;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>Creates, authenticates, and lists persistent Ultima Online accounts.</summary>
public interface IAccountService
{
    /// <summary>Creates an account with a hashed password when the username is available.</summary>
    /// <param name="username">The username to register.</param>
    /// <param name="password">The plaintext password to hash and store.</param>
    /// <param name="accountType">The account's initial access level.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The creation result, including the account on success or an error reason on failure.</returns>
    Task<AccountCreateResult> CreateAccountAsync(
        string username,
        string password,
        AccountType accountType = AccountType.Regular,
        CancellationToken cancellationToken = default
    );

    /// <summary>Authenticates an unlocked account and records its latest login time.</summary>
    /// <param name="username">The username to authenticate.</param>
    /// <param name="password">The plaintext password to verify.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The account when authentication succeeds; otherwise <see langword="null" />.</returns>
    Task<AccountEntity?> LoginAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default
    );

    /// <summary>Retrieves all stored accounts.</summary>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The stored accounts.</returns>
    Task<IEnumerable<AccountEntity>> ListAccountsAsync(CancellationToken cancellationToken = default);
}
