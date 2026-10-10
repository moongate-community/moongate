namespace Moongate.Server.Ultima.Data.Account;

/// <summary>
///     A change would leave no unlocked administrator with API access.
/// </summary>
public sealed class LastAdministratorException : InvalidOperationException
{
    public LastAdministratorException()
        : base("A change would leave no unlocked administrator with API access.")
    {
    }
}
