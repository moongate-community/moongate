using Moongate.Core.Primitives;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Ultima.Data.Account;
using Moongate.Server.Ultima.Entities.Auth;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Tests.TestSupport.Admin;

internal sealed class DelayedAccountService : IAccountService
{
    private readonly IAccountService _inner;
    public TaskCompletionSource Committed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Exception? ListFailure { get; set; }
    public bool DelayResponse { get; set; } = true;

    public DelayedAccountService(IAccountService inner)
    {
        _inner = inner;
    }

    public async Task<AccountCreateResult> CreateAccountAsync(
        AccountCreateOptions options,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _inner.CreateAccountAsync(options, cancellationToken);

        if (result.Success && DelayResponse)
        {
            Committed.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }

        return result;
    }

    public Task<AccountCreateResult> CreateAccountAsync(
        string username,
        string password,
        AccountType accountType = AccountType.Regular,
        CancellationToken cancellationToken = default
    )
        => _inner.CreateAccountAsync(username, password, accountType, cancellationToken);

    public Task<AccountEntity?> LoginAsync(string username, string password, CancellationToken cancellationToken = default)
        => _inner.LoginAsync(username, password, cancellationToken);

    public Task<IEnumerable<AccountEntity>> ListAccountsAsync(CancellationToken cancellationToken = default)
        => _inner.ListAccountsAsync(cancellationToken);

    public Task<AccountPage> ListAccountsPageAsync(
        Serial afterId,
        int pageSize = 50,
        CancellationToken cancellationToken = default
    )
        => ListFailure is null
               ? _inner.ListAccountsPageAsync(afterId, pageSize, cancellationToken)
               : Task.FromException<AccountPage>(ListFailure);
}
