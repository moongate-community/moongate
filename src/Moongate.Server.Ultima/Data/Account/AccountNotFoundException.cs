namespace Moongate.Server.Ultima.Data.Account;

/// <summary>
///     An account that does not exist was asked for.
/// </summary>
public sealed class AccountNotFoundException : KeyNotFoundException
{
    public AccountNotFoundException()
        : base("An account that does not exist was asked for.")
    {
    }
}
