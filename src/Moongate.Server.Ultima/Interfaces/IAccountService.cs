using Moongate.Core.Primitives;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Ultima.Data.Account;
using Moongate.Server.Ultima.Entities.Auth;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     Provides account creation, authentication and detached account queries.
/// </summary>
public interface IAccountService
{
    /// <summary>
    ///     Creates a game account with administration access disabled.
    /// </summary>
    /// <param name="username">
    ///     The username to register.
    /// </param>
    /// <param name="password">
    ///     The plaintext password to hash and store.
    /// </param>
    /// <param name="accountType">
    ///     The account's initial access level.
    /// </param>
    /// <param name="cancellationToken">
    ///     Cancels the database operation.
    /// </param>
    /// <returns>
    ///     The creation result, including the account on success or an error reason on failure.
    /// </returns>
    Task<AccountCreateResult> CreateAccountAsync(
        string username,
        string password,
        AccountType accountType = AccountType.Regular,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    ///     Authenticates an unlocked account and records its last login.
    /// </summary>
    /// <param name="username">
    ///     The username to authenticate.
    /// </param>
    /// <param name="password">
    ///     The plaintext password to verify.
    /// </param>
    /// <param name="cancellationToken">
    ///     Cancels the database operation.
    /// </param>
    /// <returns>
    ///     The account when authentication succeeds; otherwise <see langword="null" />.
    /// </returns>
    Task<AccountEntity?> LoginAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    ///     Returns all accounts for trusted local callers.
    /// </summary>
    /// <param name="cancellationToken">
    ///     Cancels the database operation.
    /// </param>
    /// <returns>
    ///     The stored accounts.
    /// </returns>
    Task<IEnumerable<AccountEntity>> ListAccountsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Creates an account with all initial access settings in one insert.
    /// </summary>
    /// <param name="options">
    ///     The account credentials and initial access settings.
    /// </param>
    /// <param name="cancellationToken">
    ///     Cancels the database operation.
    /// </param>
    /// <returns>
    ///     The creation result, including the account on success or an error reason on failure.
    /// </returns>
    Task<AccountCreateResult> CreateAccountAsync(
        AccountCreateOptions options,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    ///     Returns at most 200 accounts ordered after the supplied ID; zero starts the first page.
    /// </summary>
    /// <param name="afterId">
    ///     The ID of the last account already seen; zero starts the first page.
    /// </param>
    /// <param name="pageSize">
    ///     The requested page size, capped at 200.
    /// </param>
    /// <param name="cancellationToken">
    ///     Cancels the database operation.
    /// </param>
    /// <returns>
    ///     The requested page of accounts.
    /// </returns>
    Task<AccountPage> ListAccountsPageAsync(
        Serial afterId,
        int pageSize = 50,
        CancellationToken cancellationToken = default
    );
}
