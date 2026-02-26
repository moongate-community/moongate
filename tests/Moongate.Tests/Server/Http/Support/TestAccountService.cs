using Moongate.Server.Interfaces.Services.Accounting;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Http.Support;

public sealed class TestAccountService : IAccountService
{
    public Func<string, Task<bool>> CheckAccountExistsAsyncImpl { get; init; } = _ => Task.FromResult(false);

    public Func<string, string, string, AccountType, Task<UOAccountEntity?>> CreateAccountAsyncImpl { get; init; } =
        (_, _, _, _) => Task.FromResult<UOAccountEntity?>(null);

    public Func<Serial, Task<bool>> DeleteAccountAsyncImpl { get; init; } = _ => Task.FromResult(false);

    public Func<Serial, Task<UOAccountEntity?>> GetAccountAsyncImpl { get; init; } =
        _ => Task.FromResult<UOAccountEntity?>(null);

    public Func<CancellationToken, Task<IReadOnlyList<UOAccountEntity>>> GetAccountsAsyncImpl { get; init; } =
        _ => Task.FromResult<IReadOnlyList<UOAccountEntity>>([]);

    public Func<string, string, Task<UOAccountEntity?>> LoginAsyncImpl { get; init; } =
        (_, _) => Task.FromResult<UOAccountEntity?>(null);

    public Func<Serial, string?, string?, string?, AccountType?, bool?, CancellationToken, Task<UOAccountEntity?>>
        UpdateAccountAsyncImpl
    {
        get;
        init;
    } = (_, _, _, _, _, _, _) => Task.FromResult<UOAccountEntity?>(null);

    public Task<bool> CheckAccountExistsAsync(string username)
        => CheckAccountExistsAsyncImpl(username);

    public Task<UOAccountEntity?> CreateAccountAsync(
        string username,
        string password,
        string email = "",
        AccountType accountType = AccountType.Regular
    )
        => CreateAccountAsyncImpl(username, password, email, accountType);

    public Task<bool> DeleteAccountAsync(Serial accountId)
        => DeleteAccountAsyncImpl(accountId);

    public Task<UOAccountEntity?> GetAccountAsync(Serial accountId)
        => GetAccountAsyncImpl(accountId);

    public Task<IReadOnlyList<UOAccountEntity>> GetAccountsAsync(CancellationToken cancellationToken = default)
        => GetAccountsAsyncImpl(cancellationToken);

    public Task<UOAccountEntity?> LoginAsync(string username, string password)
        => LoginAsyncImpl(username, password);

    public Task<UOAccountEntity?> UpdateAccountAsync(
        Serial accountId,
        string? username = null,
        string? password = null,
        string? email = null,
        AccountType? accountType = null,
        bool? isLocked = null,
        CancellationToken cancellationToken = default
    )
        => UpdateAccountAsyncImpl(
            accountId,
            username,
            password,
            email,
            accountType,
            isLocked,
            cancellationToken
        );
}
