using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Interfaces.Commands;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Core.Types.Commands;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Types;
using Serilog;

namespace Moongate.Server.Ultima.Commands;

/// <summary>Creates accounts through an authorized command source.</summary>
public sealed class AccountCommand : ICommandExecutor
{
    private const string Usage = "Usage: account create <username> <password> [Regular|GameMaster|Administrator]";

    private readonly ILogger _logger = Log.ForContext<AccountCommand>();
    private readonly IAccountService _accounts;
    private readonly IAccountAdminAccessService? _adminAccess;

    public AccountCommand(IAccountService accounts, IAccountAdminAccessService? adminAccess = null)
    {
        _accounts = accounts;
        _adminAccess = adminAccess;
    }

    /// <inheritdoc />
    public async Task ExecuteAsync(CommandContext context)
    {
        var arguments = context.Arguments;

        if (arguments.Length > 0 && arguments[0].Equals("api-access", StringComparison.OrdinalIgnoreCase))
        {
            await SetApiAccessAsync(context);

            return;
        }

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

    private async Task SetApiAccessAsync(CommandContext context)
    {
        var args = context.Arguments;

        if (context.Source != CommandSourceType.Console)
        {
            context.PrintError("API access provisioning is available only from the local console.");

            return;
        }

        if (args.Length != 3 ||
            !(args[2].Equals("on", StringComparison.OrdinalIgnoreCase) ||
              args[2].Equals("off", StringComparison.OrdinalIgnoreCase)))
        {
            context.PrintError("Usage: account api-access <username> <on|off>");

            return;
        }

        if (_adminAccess is null)
        {
            context.PrintError("Account administration is unavailable.");

            return;
        }

        try
        {
            var enabled = args[2].Equals("on", StringComparison.OrdinalIgnoreCase);
            await _adminAccess.SetApiAccessAsync(args[1], enabled, context.CancellationToken);
            _logger.Information("Console updated account API access for {Username}; enabled {Enabled}", args[1], enabled);
            context.Print("Account API access updated.");
        }
        catch (KeyNotFoundException) { context.PrintError("Account not found."); }
        catch (OperationCanceledException) { throw; }
        catch (Exception)
        {
            _logger.Warning("Console account API access update failed");
            context.PrintError("Account API access update failed. Check server logs.");
        }
    }
}
