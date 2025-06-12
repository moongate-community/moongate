using Moongate.Core.Server.Data.Internal.Commands;
using Moongate.Core.Server.Instances;
using Moongate.Core.Server.Interfaces.Services;
using Moongate.Core.Server.Types;
using Moongate.UO.Interfaces.Services;

namespace Moongate.UO.Commands;

public class AccountCommands
{
    private readonly IAccountService _accountService;

    public AccountCommands(ICommandSystemService commandSystemService, IAccountService accountService)
    {
        _accountService = accountService;


        commandSystemService.RegisterCommand(
            "create_account",
            CreateAccount,
            "Creates a new account with the specified username, password, and account level.",
            AccountLevelType.Admin
        );
    }

    private async Task CreateAccount(CommandSystemContext context)
    {
        var level = Enum.TryParse(context.Arguments[2], true, out AccountLevelType accountType)
            ? accountType
            : AccountLevelType.User;

        if (context.Arguments.Length < 3)
        {
            context.Print("Usage: create_account <username> <password> [account_level]");
            return;
        }

        var username = context.Arguments[0];
        var password = context.Arguments[1];
        var accountId = await _accountService.CreateAccount(username, password, level);

        context.Print($"Account created successfully with ID: {accountId}");
    }
}
