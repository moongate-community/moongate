using Moongate.Server.Core.Data.Admin;

namespace Moongate.Server.Ultima.Data.Account;

/// <summary>A newly issued token returned only after account transaction commit.</summary>
public sealed class AdminLoginResult
{
    public string AccessToken { get; }
    public DateTimeOffset ExpiresAt { get; }
    public AdminAccountSnapshot Account { get; }

    public AdminLoginResult(string accessToken, DateTimeOffset expiresAt, AdminAccountSnapshot account)
    {
        AccessToken = accessToken;
        ExpiresAt = expiresAt;
        Account = account;
    }
}
