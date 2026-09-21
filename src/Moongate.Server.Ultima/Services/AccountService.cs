using Moongate.Core.Utils;
using Moongate.Persistence.Interfaces;
using Moongate.Persistence.Services;
using Npgsql;
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

    private readonly MoongatePersistenceService _persistence;

    public AccountService(IDataAccess<AccountEntity> accountDataAccess, MoongatePersistenceService persistence)
    {
        _accountDataAccess = accountDataAccess;
        _persistence = persistence;
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
                Id = await _persistence.ReserveSerialAsync<AccountEntity>("auth.account_id_seq", cancellationToken),
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
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (cancellationToken.IsCancellationRequested)
        {
            // FreeSql may wrap the provider's cancellation exception.
            throw new OperationCanceledException("Account creation canceled.", exception, cancellationToken);
        }
        catch (Exception exception) when (exception.GetBaseException() is PostgresException
                                          {
                                              SqlState: PostgresErrorCodes.UniqueViolation,
                                              ConstraintName: "ux_accounts_username"
                                          })
        {
            return new(false, AccountCreateResultType.UsernameAlreadyExists);
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
        try
        {
            var account = await _accountDataAccess
                .QueryAsync(a => a.Username == username, cancellationToken);

            AccountEntity? accountEntity = null;

            foreach (var entity in account)
            {
                accountEntity = entity;

                break;
            }

            if (accountEntity is null || accountEntity.IsLocked)
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
        catch (Exception exception) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException("Account login canceled.", exception, cancellationToken);
        }
    }
}
