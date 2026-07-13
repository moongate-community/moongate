using Moongate.Core.Primitives;
using Moongate.Network.Types;
using Moongate.Server.Data;
using Moongate.Server.Interfaces.Accounts;

namespace Moongate.Tests.Support;

/// <summary>
/// Test double for <see cref="IAccountService"/>: accepts any non-empty username and
/// password, denies everything else with incorrect-credentials.
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
    {
        return string.IsNullOrEmpty(username) ? null : new Serial(1);
    }
}
