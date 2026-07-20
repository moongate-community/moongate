using Moongate.Console.Admin.Plugin.Types;
using Moongate.Core.Types;
using Moongate.Server.Abstractions.Data;

namespace Moongate.Console.Admin.Plugin.Services.Console;

/// <summary>Turns an authentication result plus the account's level into a login outcome. Pure — no I/O.</summary>
public static class ConsoleAuth
{
    public static ConsoleAuthResultType Evaluate(AccountAuthResult auth, AccountLevelType? level, AccountLevelType minLevel)
    {
        if (!auth.Success || level is null)
        {
            return ConsoleAuthResultType.LoginFailed;
        }

        return level >= minLevel ? ConsoleAuthResultType.Allowed : ConsoleAuthResultType.InsufficientPrivileges;
    }
}
