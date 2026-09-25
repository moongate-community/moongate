using System.Security.Cryptography;
using System.Text;
using Moongate.Core.Primitives;
using Moongate.Core.Utils;
using Moongate.Persistence.Interfaces;
using Moongate.Persistence.Services;
using Moongate.Persistence.Types.Persistence;
using Moongate.Server.Core.Data.Admin;
using Moongate.Server.Core.Interfaces.Admin;
using Moongate.Server.Ultima.Data.Account;
using Moongate.Server.Ultima.Entities.Auth;
using Moongate.Server.Ultima.Interfaces;
using Serilog;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Serializes administrative authorization changes across processes using account row locks.
/// </summary>
public sealed class AccountAdminAccessService : IAccountAdminAccessService
{
    private readonly ILogger _logger = Log.ForContext<AccountAdminAccessService>();
    private readonly MoongatePersistenceService _persistence;
    private readonly IDataAccess<AccountEntity> _accounts;
    private readonly IAdminSessionStore _sessions;
    private readonly AdminSessionOptions _options;

    public AccountAdminAccessService(
        MoongatePersistenceService persistence,
        IDataAccess<AccountEntity> accounts,
        IAdminSessionStore sessions,
        AdminSessionOptions options
    )
    {
        _persistence = persistence;
        _accounts = accounts;
        _sessions = sessions;
        _options = options;
    }

    public async Task<AdminLoginResult?> LoginAsync(string username, string password, CancellationToken token = default)
    {
        var id = await FindIdAsync(username, token);

        if (id is null)
        {
            return null;
        }

        string? digest = null;
        AdminLoginResult? result = null;

        try
        {
            await _persistence.ExecuteInTransactionAsync(
                PersistenceDatabaseTarget.Accounts,
                async tx =>
                {
                    var account = await tx.GetByIdForUpdateAsync<AccountEntity>(id.Value, token);

                    if (account is null ||
                        account.IsLocked ||
                        !account.CanAccessApi ||
                        !Enum.IsDefined(account.AccountType) ||
                        !StringComparer.Ordinal.Equals(account.Username, username) ||
                        !HashUtils.VerifyPassword(password, account.HashPassword))
                    {
                        return;
                    }

                    var gate = await _sessions.ReadGateAsync(account.Id, token);

                    if (gate is null || gate.Blocked)
                    {
                        // Holding the SQL row lock proves every previous transaction has resolved.
                        // Rotate instead of reopening any generation that could have issued an old token.
                        gate = await _sessions.ResetGateAsync(account.Id, false, token);
                    }

                    var rawToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
                    digest = Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(rawToken)));
                    var session = await _sessions.IssueAsync(
                        new(account.Id, account.Username, account.AccountType),
                        gate.Generation,
                        digest,
                        _options.Lifetime,
                        token
                    );
                    account.LastLoginAt = DateTime.UtcNow;
                    await tx.GetDataAccess<AccountEntity>().UpsertAsync(account, token);
                    result = new(
                        rawToken,
                        session.ExpiresAt,
                        new(
                            account.Id,
                            account.Username,
                            account.AccountType,
                            account.CanAccessApi,
                            account.IsLocked,
                            account.CreatedAt
                        )
                    );
                },
                token
            );

            return result;
        }
        catch
        {
            if (digest is not null)
            {
                try
                {
                    await _sessions.RemoveAsync(digest, CancellationToken.None);
                }
                catch (Exception)
                {
                    // The undisclosed random token still has a strict absolute expiry.
                    _logger.Warning(
                        "Could not remove an undisclosed administrative session after a failed login transaction"
                    );
                }
            }

            throw;
        }
    }

    public async Task SetApiAccessAsync(string username, bool enabled, CancellationToken token = default)
    {
        var id = await FindIdAsync(username, token) ?? throw new KeyNotFoundException("Account not found.");
        await MutateAsync(id, account => account.CanAccessApi = enabled, token);
    }

    public Task UpdateAccessAsync(Serial accountId, AccountAccessOptions options, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!Enum.IsDefined(options.AccountType))
        {
            throw new ArgumentOutOfRangeException(nameof(options));
        }

        return MutateAsync(
            accountId,
            account =>
            {
                account.AccountType = options.AccountType;
                account.CanAccessApi = options.CanAccessApi;
                account.IsLocked = options.IsLocked;
            },
            token
        );
    }

    public Task ChangePasswordAsync(Serial accountId, string password, CancellationToken token = default)
    {
        var hash = HashUtils.HashPassword(password);

        return MutateAsync(accountId, account => account.HashPassword = hash, token);
    }

    public Task RevokeSessionsAsync(Serial accountId, CancellationToken token = default)
    {
        return _persistence.ExecuteInTransactionAsync(
            PersistenceDatabaseTarget.Accounts,
            async tx =>
            {
                _ = await tx.GetByIdForUpdateAsync<AccountEntity>(accountId, token) ??
                    throw new KeyNotFoundException("Account not found.");
                await _sessions.ResetGateAsync(accountId, false, token);
            },
            token
        );
    }

    private async Task<Serial?> FindIdAsync(string username, CancellationToken token)
    {
        var accounts = await _accounts.QueryAsync(a => a.Username == username, 0, 1, token);

        return accounts.Count == 0 ? null : accounts[0].Id;
    }

    private async Task MutateAsync(Serial accountId, Action<AccountEntity> mutation, CancellationToken token)
    {
        AdminAccountGate? fenced = null;
        await _persistence.ExecuteInTransactionAsync(
            PersistenceDatabaseTarget.Accounts,
            async tx =>
            {
                var account = await tx.GetByIdForUpdateAsync<AccountEntity>(accountId, token) ??
                              throw new KeyNotFoundException("Account not found.");
                fenced = await _sessions.ResetGateAsync(accountId, true, token);
                mutation(account);
                account.UpdatedAt = DateTime.UtcNow;
                await tx.GetDataAccess<AccountEntity>().UpsertAsync(account, token);
            },
            token
        );

        // Reacquire after commit. Never reopen in finally: an unknown SQL outcome must stay fenced.
        await _persistence.ExecuteInTransactionAsync(
            PersistenceDatabaseTarget.Accounts,
            async tx =>
            {
                var account = await tx.GetByIdForUpdateAsync<AccountEntity>(accountId, token);

                if (account is not null && !account.IsLocked && account.CanAccessApi && Enum.IsDefined(account.AccountType))
                {
                    await _sessions.TryOpenGateAsync(accountId, fenced!.Generation, token);
                }
            },
            token
        );
    }
}
