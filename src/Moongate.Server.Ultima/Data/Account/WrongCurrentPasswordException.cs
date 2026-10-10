namespace Moongate.Server.Ultima.Data.Account;

/// <summary>
///     The current password given for the account is not its password.
/// </summary>
public sealed class WrongCurrentPasswordException : InvalidOperationException
{
    public WrongCurrentPasswordException()
        : base("The current password given for the account is not its password.")
    {
    }
}
