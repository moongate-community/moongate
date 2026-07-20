using Moongate.Core.Primitives;
using Moongate.Core.Types;
using Moongate.Network.Types;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Data;
using Moongate.Server.Abstractions.Interfaces.Accounts;
using Moongate.Server.Abstractions.Types;

namespace Moongate.Tests.Support;

/// <summary>
/// Focused <see cref="IAccountService" /> double for console tests: seed usernames with a plaintext
/// password and a level; only the login surface (<see cref="Authenticate" />, <see cref="GetByUsername" />,
/// <see cref="GetById" />) is real, the rest throw so a test that reaches them says so.
/// </summary>
public sealed class SeededAccountService : IAccountService
{
    private readonly Dictionary<string, (string Password, AccountLevelType Level, Serial Id)> _accounts =
        new(StringComparer.OrdinalIgnoreCase);

    public void Seed(string username, string password, AccountLevelType level)
        => _accounts[username] = (password, level, (Serial)(uint)(_accounts.Count + 1));

    public AccountAuthResult Authenticate(string username, string password)
        => _accounts.TryGetValue(username, out var account) && account.Password == password
            ? AccountAuthResult.Ok(username)
            : AccountAuthResult.Denied(LoginDeniedReasonType.IncorrectCredentials);

    public AccountEntity? GetByUsername(string username)
        => _accounts.TryGetValue(username, out var account)
            ? new AccountEntity { Id = account.Id, Username = username, AccountLevel = account.Level, IsActive = true }
            : null;

    public AccountEntity? GetById(Serial accountId)
    {
        foreach (var (username, account) in _accounts)
        {
            if (account.Id == accountId)
            {
                return new AccountEntity { Id = account.Id, Username = username, AccountLevel = account.Level, IsActive = true };
            }
        }

        return null;
    }

    public Serial? GetAccountIdByUsername(string username)
        => _accounts.TryGetValue(username, out var account) ? account.Id : null;

    public AccountCreateResultType Create(string username, string password, string? email, AccountLevelType level)
        => throw new NotSupportedException();

    public AccountDeleteResultType Delete(string username)
        => throw new NotSupportedException();

    public IReadOnlyList<AccountEntity> GetAll()
        => throw new NotSupportedException();

    public IReadOnlyList<string> GetUsernames()
        => throw new NotSupportedException();

    public bool SetActive(string username, bool isActive)
        => throw new NotSupportedException();

    public bool SetLevel(string username, AccountLevelType level)
        => throw new NotSupportedException();

    public bool SetPassword(string username, string password)
        => throw new NotSupportedException();
}
