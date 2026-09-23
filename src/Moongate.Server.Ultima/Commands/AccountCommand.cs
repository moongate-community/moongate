using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Interfaces.Commands;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Types;

namespace Moongate.Server.Ultima.Commands;

/// <summary>Creates accounts through an authorized command source.</summary>
public sealed class AccountCommand : ICommandExecutor
{
    private const string Usage = "Usage: account create <username> <password> [Regular|GameMaster|Administrator]";

    private readonly IAccountService _accounts;

    public AccountCommand(IAccountService accounts)
    {
        _accounts = accounts;
    }

    /// <inheritdoc />
    public async Task ExecuteAsync(CommandContext context)
    {
        var arguments = context.Arguments;

        if (arguments.Length is not (3 or 4) ||
            !string.Equals(arguments[0], "create", StringComparison.OrdinalIgnoreCase))
        {
            context.PrintError(Usage);

            return;
        }

        var accountType = AccountType.Regular;

        if (arguments.Length == 4 &&
            (!Enum.TryParse(arguments[3], true, out accountType) || !Enum.IsDefined(accountType)))
        {
            context.PrintError(Usage);

            return;
        }

        var username = arguments[1];
        var result = await _accounts.CreateAccountAsync(
                         username,
                         arguments[2],
                         accountType,
                         context.CancellationToken
                     );

        if (result.Success)
        {
            context.Print("Account '{0}' created ({1}).", username, accountType);
        }
        else if (result.ResultType == AccountCreateResultType.UsernameAlreadyExists)
        {
            context.PrintError("Account '{0}' already exists.", username);
        }
        else
        {
            context.PrintError("Account creation failed. Check server logs.");
        }
    }
}
