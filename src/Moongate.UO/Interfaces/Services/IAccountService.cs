using Moongate.Core.Data;
using Moongate.Core.Server.Interfaces.Services.Base;
using Moongate.Core.Server.Types;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.UO.Interfaces.Services;

public interface IAccountService : IMoongateAutostartService
{
    Task<string> CreateAccount(string username, string password, AccountLevelType accountLevel = AccountLevelType.User);

    Task<bool> ChangePassword(string accountName, string newPassword);

    Task<bool> ChangeLevel(string accountName, AccountLevelType levelType);

    Task<Result<UOAccountEntity>> LoginAsync(string username, string password);
}
