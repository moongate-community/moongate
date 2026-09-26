using Moongate.Core.Primitives;
using Moongate.Server.Core.Types.Accounts;

namespace Moongate.Server.Core.Data.Admin;

/// <summary>
///     Immutable administration identity snapshot.
/// </summary>
public sealed class AdminIdentity
{
    public Serial AccountId { get; }
    public string Username { get; }
    public AccountType AccountType { get; }

    public AdminIdentity(Serial accountId, string username, AccountType accountType)
    {
        AccountId = accountId;
        Username = username;
        AccountType = accountType;
    }
}
