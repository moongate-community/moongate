using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Data.Localization;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Commands;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Core.Types.Commands;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Types;
using Serilog;

namespace Moongate.Server.Ultima.Commands;

/// <summary>
///     Creates accounts through an authorized command source.
/// </summary>
public sealed class AccountCommand : ICommandExecutor, ICommandArgumentCompleter
{
    private const string Syntax = "account create <username> <password> [Regular|GameMaster|Administrator]";

    private readonly ILogger _logger = Log.ForContext<AccountCommand>();
    private readonly IAccountService _accounts;
    private readonly IAccountAdminAccessService? _adminAccess;
    private readonly ILocalizationService? _localization;

    public AccountCommand(
        IAccountService accounts,
        IAccountAdminAccessService? adminAccess = null,
        ILocalizationService? localization = null
    )
    {
        _localization = localization;
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
            context.PrintError(_localization.Text(CommandMessages.Usage, "Usage: {0}", Syntax));

            return;
        }

        var accountType = AccountType.Regular;

        if (arguments.Length == 4 &&
            (!Enum.TryParse(arguments[3], true, out accountType) || !Enum.IsDefined(accountType)))
        {
            context.PrintError(_localization.Text(CommandMessages.Usage, "Usage: {0}", Syntax));

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
            context.Print(
                _localization.Text(CommandMessages.AccountCreated, "Account '{0}' created ({1}).", username, accountType)
            );
        }
        else if (result.ResultType == AccountCreateResultType.UsernameAlreadyExists)
        {
            context.PrintError(_localization.Text(CommandMessages.AccountExists, "An account by that name already exists!"));
        }
        else
        {
            context.PrintError(
                _localization.Text(
                    CommandMessages.AccountCreationFailed,
                    "The account creation failed. Check the server logs."
                )
            );
        }
    }

    /// <inheritdoc />
    /// <remarks>
    ///     The user name and the password are never offered.
    /// </remarks>
    public IReadOnlyList<string> GetArgumentCompletions(IReadOnlyList<string> previousArguments)
    {
        return previousArguments switch
        {
            [] => ["create", "api-access"],
            [var action, _, _] when action.Equals("create", StringComparison.OrdinalIgnoreCase) =>
                Enum.GetNames<AccountType>(),
            [var action, _] when action.Equals("api-access", StringComparison.OrdinalIgnoreCase) => ["on", "off"],
            _                                                                                    => []
        };
    }

    private async Task SetApiAccessAsync(CommandContext context)
    {
        var args = context.Arguments;

        if (context.Source != CommandSourceType.Console)
        {
            context.PrintError(
                _localization.Text(CommandMessages.LocalConsoleOnly, "This is available only from the local console.")
            );

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
        catch (KeyNotFoundException)
        {
            context.PrintError("Account not found.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            _logger.Warning("Console account API access update failed");
            context.PrintError("Account API access update failed. Check server logs.");
        }
    }
}
