using Moongate.Server.Attributes;
using Moongate.Server.Data.Internal.Commands;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Services.Accounting;
using Moongate.Server.Interfaces.Services.Console;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Types.Commands;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Commands.Accounts;

/// <summary>
/// Changes account passwords for the current user or, for privileged users, another user.
/// </summary>
[RegisterConsoleCommand(
    "password",
    "Changes your password or, for staff, another account password.",
    CommandSourceType.Console | CommandSourceType.InGame,
    AccountType.Regular
)]
public sealed class PasswordCommand : ICommandExecutor
{
    private readonly IAccountService _accountService;
    private readonly IGameNetworkSessionService _gameNetworkSessionService;

    public PasswordCommand(IAccountService accountService, IGameNetworkSessionService gameNetworkSessionService)
    {
        _accountService = accountService;
        _gameNetworkSessionService = gameNetworkSessionService;
    }

    public async Task ExecuteCommandAsync(CommandSystemContext context)
    {
        if (context.Source == CommandSourceType.Console)
        {
            await ExecuteConsoleAsync(context);

            return;
        }

        await ExecuteInGameAsync(context);
    }

    private async Task ExecuteConsoleAsync(CommandSystemContext context)
    {
        if (context.Arguments.Length != 2)
        {
            context.Print("Usage: password <username> <newPassword>");

            return;
        }

        await UpdateByUsernameAsync(context, context.Arguments[0], context.Arguments[1]);
    }

    private async Task ExecuteInGameAsync(CommandSystemContext context)
    {
        if (!_gameNetworkSessionService.TryGet(context.SessionId, out var session))
        {
            context.PrintError("Failed to update password: no active session found.");

            return;
        }

        if (session.AccountType >= AccountType.GameMaster)
        {
            await ExecutePrivilegedInGameAsync(context, session);

            return;
        }

        if (context.Arguments.Length != 1)
        {
            context.Print("Usage: .password <newPassword>");

            return;
        }

        await UpdateOwnPasswordAsync(context, session, context.Arguments[0]);
    }

    private async Task ExecutePrivilegedInGameAsync(CommandSystemContext context, GameSession session)
    {
        if (context.Arguments.Length == 1)
        {
            await UpdateOwnPasswordAsync(context, session, context.Arguments[0]);

            return;
        }

        if (context.Arguments.Length != 2)
        {
            context.Print("Usage: .password <newPassword>");
            context.Print("Usage: .password <username> <newPassword>");

            return;
        }

        await UpdateByUsernameAsync(context, context.Arguments[0], context.Arguments[1]);
    }

    private async Task UpdateOwnPasswordAsync(CommandSystemContext context, GameSession session, string newPassword)
    {
        if (session.AccountId == Serial.Zero)
        {
            context.PrintError("Failed to update password: no account is associated with this session.");

            return;
        }

        if (string.IsNullOrWhiteSpace(newPassword))
        {
            context.PrintError("Password cannot be empty.");

            return;
        }

        var updatedAccount = await _accountService.UpdateAccountAsync(session.AccountId, password: newPassword);

        if (updatedAccount is null)
        {
            context.PrintError("Failed to update password for the current account.");

            return;
        }

        context.Print("Password updated for account '{0}'.", updatedAccount.Username);
    }

    private async Task UpdateByUsernameAsync(CommandSystemContext context, string username, string newPassword)
    {
        if (string.IsNullOrWhiteSpace(newPassword))
        {
            context.PrintError("Password cannot be empty.");

            return;
        }

        var account = await ResolveAccountByUsernameAsync(username);

        if (account is null)
        {
            context.PrintError("Account '{0}' was not found.", username);

            return;
        }

        var updatedAccount = await _accountService.UpdateAccountAsync(account.Id, password: newPassword);

        if (updatedAccount is null)
        {
            context.PrintError("Failed to update password for account '{0}'.", username);

            return;
        }

        context.Print("Password updated for account '{0}'.", updatedAccount.Username);
    }

    private async Task<UOAccountEntity?> ResolveAccountByUsernameAsync(string username)
    {
        var accounts = await _accountService.GetAccountsAsync();

        return accounts.FirstOrDefault(
            account => string.Equals(account.Username, username, StringComparison.OrdinalIgnoreCase)
        );
    }
}
