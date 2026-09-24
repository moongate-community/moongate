using Moongate.Core.Primitives;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Ultima.Data.Account;
using Moongate.Server.Ultima.Entities.Auth;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>Provides account creation, authentication and detached account queries.</summary>
public interface IAccountService
{
    /// <summary>Creates a game account with administration access disabled.</summary>
    Task<AccountCreateResult> CreateAccountAsync(
        string username,
        string password,
        AccountType accountType = AccountType.Regular,
        CancellationToken cancellationToken = default
    );

    /// <summary>Authenticates an unlocked account and records its last login.</summary>
    Task<AccountEntity?> LoginAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default
    );

    /// <summary>Returns all accounts for trusted local callers.</summary>
    Task<IEnumerable<AccountEntity>> ListAccountsAsync(CancellationToken cancellationToken = default);

    /// <summary>Creates an account with all initial access settings in one insert.</summary>
    Task<AccountCreateResult> CreateAccountAsync(
        AccountCreateOptions options,
        CancellationToken cancellationToken = default
    );

    /// <summary>Returns at most 200 accounts ordered after the supplied ID; zero starts the first page.</summary>
    Task<AccountPage> ListAccountsPageAsync(
        Serial afterId,
        int pageSize = 50,
        CancellationToken cancellationToken = default
    );
}
