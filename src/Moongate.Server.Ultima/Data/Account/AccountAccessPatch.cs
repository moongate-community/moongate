using Moongate.Server.Core.Types.Accounts;

namespace Moongate.Server.Ultima.Data.Account;

/// <summary>
///     A change of the security settings of an account in which only the fields that are set change.
/// </summary>
public sealed class AccountAccessPatch
{
    /// <summary>
    ///     Gets the new lock, or null to leave it as it is.
    /// </summary>
    public bool? IsLocked { get; init; }

    /// <summary>
    ///     Gets the new API access, or null to leave it as it is.
    /// </summary>
    public bool? CanAccessApi { get; init; }

    /// <summary>
    ///     Gets the new account type, or null to leave it as it is.
    /// </summary>
    public AccountType? AccountType { get; init; }
}
