using Moongate.Server.Interfaces.Services.Accounting;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Support;

public sealed class CommandSystemTestAccountService : IAccountService
{
    public bool AccountExists { get; set; }

    public bool CreateCalled { get; private set; }

    public string? CreatedUsername { get; private set; }

    public string? CreatedPassword { get; private set; }

    public string? CreatedEmail { get; private set; }

    public AccountType CreatedAccountType { get; private set; } = AccountType.Regular;

    public Task<bool> CheckAccountExistsAsync(string username)
        => Task.FromResult(AccountExists);

    public Task<UOAccountEntity?> CreateAccountAsync(
        string username,
        string password,
        string email,
        AccountType accountType = AccountType.Regular
    )
    {
        CreateCalled = true;
        CreatedUsername = username;
        CreatedPassword = password;
        CreatedEmail = email;
        CreatedAccountType = accountType;

        return Task.FromResult<UOAccountEntity?>(
            new UOAccountEntity
            {
                Id = (Serial)1,
                Username = username,
                PasswordHash = password,
                Email = email,
                AccountType = accountType
            }
        );
    }

    public Task<bool> DeleteAccountAsync(Serial accountId)
        => Task.FromResult(true);

    public Task<UOAccountEntity?> GetAccountAsync(Serial accountId)
        => Task.FromResult<UOAccountEntity?>(null);

    public Task<IReadOnlyList<UOAccountEntity>> GetAccountsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<UOAccountEntity>>([]);

    public Task<UOAccountEntity?> UpdateAccountAsync(
        Serial accountId,
        string? username = null,
        string? password = null,
        string? email = null,
        AccountType? accountType = null,
        bool? isLocked = null,
        CancellationToken cancellationToken = default
    )
        => Task.FromResult<UOAccountEntity?>(null);

    public Task<UOAccountEntity?> LoginAsync(string username, string password)
        => Task.FromResult<UOAccountEntity?>(null);
}
