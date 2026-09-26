using Moongate.Core.Primitives;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Ultima.Data.Account;
using Moongate.Server.Ultima.Entities.Auth;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Types;

namespace Moongate.Tests.TestSupport.Server.Ultima;

internal sealed class RecordingAccountService : IAccountService
{
    public AccountCreateResult Result { get; set; } = new(true, AccountCreateResultType.Success);

    public AccountEntity? LoginResult { get; set; }

    public int CreateCount { get; private set; }

    public string? Username { get; private set; }

    public string? Password { get; private set; }

    public AccountType? RequestedAccountType { get; private set; }

    public CancellationToken CancellationToken { get; private set; }

    public Task<AccountCreateResult> CreateAccountAsync(
        string username,
        string password,
        AccountType accountType = AccountType.Regular,
        CancellationToken cancellationToken = default
    )
    {
        CreateCount++;
        Username = username;
        Password = password;
        RequestedAccountType = accountType;
        CancellationToken = cancellationToken;

        return Task.FromResult(Result);
    }

    public Task<AccountEntity?> LoginAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default
    )
    {
        return Task.FromResult(LoginResult);
    }

    public Task<IEnumerable<AccountEntity>> ListAccountsAsync(CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }

    public Task<AccountCreateResult> CreateAccountAsync(
        AccountCreateOptions options,
        CancellationToken cancellationToken = default
    )
    {
        throw new NotSupportedException();
    }

    public Task<AccountPage> ListAccountsPageAsync(
        Serial afterId,
        int pageSize = 50,
        CancellationToken cancellationToken = default
    )
    {
        throw new NotSupportedException();
    }
}
