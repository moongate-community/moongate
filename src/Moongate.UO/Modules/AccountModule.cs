using Moongate.Core.Server.Attributes.Scripts;
using Moongate.Core.Server.Types;
using Moongate.UO.Interfaces.Services;

namespace Moongate.UO.Modules;

[ScriptModule("accounts")]
public class AccountModule
{
    private readonly IAccountService _accountService;

    public AccountModule(IAccountService accountService)
    {
        _accountService = accountService;
    }

    [ScriptFunction("Create new account")]
    public void CreateAccount(string username, string password, string accountLevel = "user")
    {
        var level = Enum.TryParse(accountLevel, true, out AccountLevelType accountType)
            ? accountType
            : AccountLevelType.User;
        _accountService.CreateAccount(username, password, level).GetAwaiter().GetResult();
    }


    [ScriptFunction("Change password of account")]
    public bool ChangePassword(string accountName, string newPassword)
    {
        return _accountService.ChangePassword(accountName, newPassword).Result;
    }
}
