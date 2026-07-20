namespace Moongate.Console.Admin.Plugin.Types;

/// <summary>Outcome of a console login attempt.</summary>
public enum ConsoleAuthResultType
{
    Allowed,
    LoginFailed,
    InsufficientPrivileges
}
