using Moongate.Core.Primitives;
using Moongate.Server.Core.Types.Accounts;

namespace Moongate.Server.Core.Data.Admin;

/// <summary>A safe account summary without credentials or private contact data.</summary>
public sealed class AdminAccountSnapshot
{
    public Serial AccountId { get; }
    public string Username { get; }
    public AccountType AccountType { get; }
    public bool CanAccessApi { get; }
    public bool IsLocked { get; }
    public DateTime CreatedAt { get; }

    public AdminAccountSnapshot(
        Serial accountId,
        string username,
        AccountType accountType,
        bool canAccessApi,
        bool isLocked,
        DateTime createdAt
    )
    {
        AccountId = accountId;
        Username = username;
        AccountType = accountType;
        CanAccessApi = canAccessApi;
        IsLocked = isLocked;
        CreatedAt = createdAt;
    }
}
