using Moongate.Core.Primitives;
using Moongate.Core.Utils;
using Moongate.Persistence.Interfaces;
using Moongate.Persistence.Services;
using Moongate.Persistence.Types.Persistence;
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

    public Task<AccountCreateResult> CreateAccountAsync(string username, string password,
        AccountType accountType = AccountType.Regular, CancellationToken cancellationToken = default)
        => CreateAccountAsync(new AccountCreateOptions
        {
            Username = username, Password = password, AccountType = accountType
        }, cancellationToken);

    public async Task<AccountPage> ListAccountsPageAsync(Serial afterId, int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        if (pageSize is < 1 or > 200)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize));
        }
        var values = await _accountDataAccess.QueryAsync(a => a.Id > afterId, 0, pageSize + 1, cancellationToken);
        var items = values.Take(pageSize).ToArray();
        return new(items, values.Count > pageSize ? items[^1].Id : Serial.Zero);
    }

    public async Task<AccountCreateResult> CreateAccountAsync(AccountCreateOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!Enum.IsDefined(options.AccountType))
        {
            throw new ArgumentOutOfRangeException(nameof(options));
        }
        var username = options.Username;
        var password = options.Password;
        var accountType = options.AccountType;
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
                CanAccessApi = options.CanAccessApi,
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
            var matches = await _accountDataAccess.QueryAsync(a => a.Username == username, 0, 1, cancellationToken);
            Serial? id = matches.Count == 0 ? null : matches[0].Id;
            if (id is null)
            {
                return null;
            }
            AccountEntity? result = null;
            await _persistence.ExecuteInTransactionAsync(PersistenceDatabaseTarget.Accounts, async tx =>
            {
                var account = await tx.GetByIdForUpdateAsync<AccountEntity>(id.Value, cancellationToken);
                if (account is null || account.IsLocked || !StringComparer.Ordinal.Equals(account.Username, username) ||
                    !HashUtils.VerifyPassword(password, account.HashPassword))
                {
                    return;
                }
                account.LastLoginAt = DateTime.UtcNow;
                await tx.GetDataAccess<AccountEntity>().UpsertAsync(account, cancellationToken);
                result = account;
            }, cancellationToken);
            return result;
        }
        catch (Exception exception) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException("Account login canceled.", exception, cancellationToken);
        }
    }

    public async Task<IEnumerable<AccountEntity>> ListAccountsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var accounts = await _accountDataAccess.GetAllAsync(cancellationToken);

            return accounts;
        }
        catch (Exception exception) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException("Listing accounts canceled.", exception, cancellationToken);
        }
    }
}
