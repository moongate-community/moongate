using Moongate.Core.Server.Interfaces.Services.Base;
using Moongate.Core.Server.Types;

namespace Moongate.UO.Interfaces.Services;

public interface IAccountService : IMoongateAutostartService
{
    Task<string> CreateAccount(string username, string password, AccountLevelType accountLevel = AccountLevelType.User);



}
