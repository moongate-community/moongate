using Moongate.Persistence.Entities;
using SquidStd.Scripting.Lua.Interfaces.Scripts;

namespace Moongate.Server.Scripting.Views;

/// <summary>Projects an <see cref="AccountEntity" /> into the Lua field table returned by <c>account.get</c>.</summary>
public sealed record AccountLuaView(AccountEntity Account) : ILuaTable
{
    public Dictionary<string, object?> ToDictionary()
        => new()
        {
            ["id"] = Account.Id.Value,
            ["username"] = Account.Username,
            ["email"] = Account.Email,
            ["level"] = Account.AccountLevel.ToString(),
            ["is_active"] = Account.IsActive,
            ["mobiles"] = Account.MobileIds.Select(id => id.Value).ToList()
        };
}
