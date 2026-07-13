using Moongate.Core.Primitives;
using Moongate.Server.Data;

namespace Moongate.Server.Interfaces.Accounts;

/// <summary>Authenticates account credentials. Persistence is wired later; the stub accepts non-empty credentials.</summary>
public interface IAccountService
{
    AccountAuthResult Authenticate(string username, string password);

    Serial? GetAccountIdByUsername(string username);

}
