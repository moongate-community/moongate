using Moongate.Core.Primitives;
using Moongate.Network.Types;
using Moongate.Persistence.Entities;
using Moongate.Server.Data;
using Moongate.Server.Interfaces.Accounts;
using Serilog;
using SquidStd.Core.Utils;
using SquidStd.Persistence.Abstractions.Interfaces.Persistence;

namespace Moongate.Server.Services.Accounts;

public class AccountService : IAccountService
{
    private readonly ILogger _logger = Log.ForContext<AccountService>();

    private readonly IEntityStore<AccountEntity, Serial> _accountStore;

    public AccountService(IPersistenceService persistenceService)
    {
        _accountStore = persistenceService.GetStore<AccountEntity, Serial>();
    }

    public AccountAuthResult Authenticate(string username, string password)
    {
        var account = _accountStore
                      .Query()
                      .FirstOrDefault(s => s.Username == username && HashUtils.VerifyPassword(password, s.PasswordHash));

        if (account == null)
        {
            return new AccountAuthResult { Success = false, Reason = LoginDeniedReasonType.BadCredentials };
        }

        if (!account.IsActive)
        {
            return new AccountAuthResult { Success = false, Reason = LoginDeniedReasonType.AccountBlocked };
        }

        return new AccountAuthResult { Success = true, Username = account.Username };
    }

    public Serial? GetAccountIdByUsername(string username)
    {
        return _accountStore.Query().FirstOrDefault(s => s.Username == username)?.Id;
    }
}
