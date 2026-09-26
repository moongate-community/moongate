using Moongate.Server.Core.Types.Accounts;

namespace Moongate.Server.Ultima.Data.Account;

/// <summary>
///     Security settings applied together while fencing administrative sessions.
/// </summary>
public sealed class AccountAccessOptions
{
    public bool IsLocked { get; init; }
    public bool CanAccessApi { get; init; }
    public AccountType AccountType { get; init; }
}
