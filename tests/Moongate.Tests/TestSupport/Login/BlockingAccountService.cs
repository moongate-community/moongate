using Moongate.Core.Primitives;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Ultima.Data.Account;
using Moongate.Server.Ultima.Entities.Auth;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Tests.TestSupport.Login;

internal sealed class BlockingAccountService : IAccountService
{
    private readonly TaskCompletionSource<AccountEntity?> _result =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public void Release(AccountEntity? account)
        => _result.TrySetResult(account);

    public Task<AccountEntity?> LoginAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default
    )
    {
        Entered.TrySetResult();

        return _result.Task;
    }

    public Task<AccountCreateResult> CreateAccountAsync(
        string username,
        string password,
        AccountType accountType = AccountType.Regular,
        CancellationToken cancellationToken = default
    )
        => throw new NotSupportedException();

    public Task<IEnumerable<AccountEntity>> ListAccountsAsync(CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<AccountCreateResult> CreateAccountAsync(
        AccountCreateOptions options,
        CancellationToken cancellationToken = default
    )
        => throw new NotSupportedException();

    public Task<AccountPage> ListAccountsPageAsync(
        Serial afterId,
        int pageSize = 50,
        CancellationToken cancellationToken = default
    )
        => throw new NotSupportedException();
}
