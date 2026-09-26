using Moongate.Server.Core.Types.Accounts;

namespace Moongate.Server.Ultima.Data.Account;

/// <summary>
///     Initial account values persisted together in one insert.
/// </summary>
public sealed class AccountCreateOptions
{
    public string Username { get; init; } = "";
    public string Password { get; init; } = "";
    public AccountType AccountType { get; init; } = AccountType.Regular;
    public bool CanAccessApi { get; init; }
}
