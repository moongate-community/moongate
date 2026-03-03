using Moongate.Server.Attributes;
using Moongate.Server.Data.Internal.Commands;
using Moongate.Server.Interfaces.Services.Accounting;
using Moongate.Server.Interfaces.Services.Console;
using Moongate.Server.Types.Commands;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Commands;

/// <summary>
/// Creates a new account.
/// </summary>
[RegisterConsoleCommand(
    "add_user",
    "Creates a new account: add_user <username> <password> <email> [level].",
    CommandSourceType.Console | CommandSourceType.InGame
)]
public sealed class AddUserCommand : ICommandExecutor
{
    private readonly IAccountService _accountService;

    public AddUserCommand(IAccountService accountService)
    {
        _accountService = accountService;
    }

    public async Task ExecuteCommandAsync(CommandSystemContext context)
    {
        if (context.Arguments.Length is < 3 or > 4)
        {
            context.Print("Usage: add_user <username> <password> <email> [level]");

            return;
        }

        var username = context.Arguments[0];
        var password = context.Arguments[1];
        var email = context.Arguments[2];
        var level = AccountType.Regular;

        if (context.Arguments.Length == 4 &&
            !Enum.TryParse(context.Arguments[3], true, out level))
        {
            var validLevels = string.Join(", ", Enum.GetNames<AccountType>());
            context.Print("Invalid account level '{0}'. Valid levels: {1}.", context.Arguments[3], validLevels);

            return;
        }

        if (await _accountService.CheckAccountExistsAsync(username))
        {
            context.Print("User '{0}' already exists.", username);

            return;
        }

        await _accountService.CreateAccountAsync(username, password, email, level);
        context.Print("User '{0}' created with level '{1}'.", username, level);
    }
}
