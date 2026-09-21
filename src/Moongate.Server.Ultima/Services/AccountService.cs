using Moongate.Core.Utils;
using Moongate.Persistence.Interfaces;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Ultima.Data.Account;
using Moongate.Server.Ultima.Entities.Auth;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Types;
using Serilog;

namespace Moongate.Server.Ultima.Services;

public class AccountService : IAccountService
{
    private readonly ILogger _logger = Log.ForContext<AccountService>();

    private readonly IDataAccess<AccountEntity> _accountDataAccess;

    public AccountService(IDataAccess<AccountEntity> accountDataAccess)
    {
        _accountDataAccess = accountDataAccess;
    }

    public async Task<AccountCreateResult> CreateAccountAsync(
        string username,
        string password,
        AccountType accountType = AccountType.Regular,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var existingAccount = await _accountDataAccess
                                      .QueryAsync(a => a.Username == username, cancellationToken);

            if (existingAccount.Any())
            {
                return new AccountCreateResult(
                    success: false,
                    resultType: AccountCreateResultType.UsernameAlreadyExists,
                    account: null
                );
            }

            var hashedPassword = HashUtils.HashPassword(password);

            var newAccount = new AccountEntity
            {
                Username = username,
                HashPassword = hashedPassword,
                AccountType = accountType,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsLocked = false
            };

            await _accountDataAccess.UpsertAsync(newAccount, cancellationToken);

            return new AccountCreateResult(
                success: true,
                resultType: AccountCreateResultType.Success,
                account: newAccount
            );
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error creating account for username: {Username}", username);

            return new AccountCreateResult(
                success: false,
                resultType: AccountCreateResultType.Error,
                account: null,
                exception: ex
            );
        }
    }

    public async Task<AccountEntity?> LoginAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default
    )
    {
        var account = await _accountDataAccess
                          .QueryAsync(a => a.Username == username, cancellationToken);

        AccountEntity? accountEntity = null;

        foreach (var entity in account)
        {
            accountEntity = entity;

            break;
        }

        if (accountEntity == null)
        {
            return null;
        }

        if (!HashUtils.VerifyPassword(password, accountEntity.HashPassword))
        {
            _logger.Debug("Invalid login attempt: {Username}", username);

            return null;
        }

        accountEntity.LastLoginAt = DateTime.UtcNow;

        await _accountDataAccess.UpsertAsync(accountEntity, cancellationToken);

        return accountEntity;
    }
}
