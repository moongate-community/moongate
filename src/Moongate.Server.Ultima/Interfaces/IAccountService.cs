using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Ultima.Data.Account;
using Moongate.Server.Ultima.Entities.Auth;

namespace Moongate.Server.Ultima.Interfaces;

public interface IAccountService
{
    Task<AccountCreateResult> CreateAccountAsync(
        string username,
        string password,
        AccountType accountType = AccountType.Regular,
        CancellationToken cancellationToken = default
    );


    Task<AccountEntity?> LoginAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default
    );

    Task<IEnumerable<AccountEntity>> ListAccountsAsync(CancellationToken cancellationToken = default);
}
