using Moongate.Core.Primitives;
using Moongate.Core.Types;
using Moongate.Network.Types;
using Moongate.Persistence.Entities;
using Moongate.Server.Data;
using Moongate.Server.Interfaces.Accounts;
using Moongate.Server.Types;

namespace Moongate.Tests.Support;

/// <summary>
/// Test double for <see cref="IAccountService" />: accepts any non-empty username and
/// password, denies everything else with incorrect-credentials. Only the login surface is
/// stubbed — the management side throws, so a test that reaches it says so instead of
/// quietly reading a lie.
/// </summary>
public sealed class StubAccountService : IAccountService
{
    public AccountAuthResult Authenticate(string username, string password)
    {
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            return AccountAuthResult.Denied(LoginDeniedReasonType.IncorrectCredentials);
        }

        return AccountAuthResult.Ok(username);
    }

    public Serial? GetAccountIdByUsername(string username)
        => string.IsNullOrEmpty(username) ? null : new Serial(1);

    public AccountEntity? GetByUsername(string username)
        => throw new NotSupportedException();

    public IReadOnlyList<string> GetUsernames()
        => throw new NotSupportedException();

    public AccountCreateResultType Create(string username, string password, string? email, AccountLevelType level)
        => throw new NotSupportedException();

    public bool SetPassword(string username, string password)
        => throw new NotSupportedException();

    public bool SetLevel(string username, AccountLevelType level)
        => throw new NotSupportedException();

    public bool SetActive(string username, bool isActive)
        => throw new NotSupportedException();

    public AccountDeleteResultType Delete(string username)
        => throw new NotSupportedException();
}
