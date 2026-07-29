using Moongate.Persistence.Entities;
using SquidStd.Scripting.Lua.Interfaces.Scripts;

namespace Moongate.Server.Scripting.Views;

/// <summary>
/// Projects an <see cref="ItemEntity" /> into the Lua field table returned by <c>item.get</c>.
/// <paramref name="DisplayName" /> is resolved by the caller, because an item the shard does not
/// name is named by the client and a record cannot reach a service to ask.
/// </summary>
public sealed record ItemLuaView(ItemEntity Item, string DisplayName) : ILuaTable
{
    public Dictionary<string, object?> ToDictionary()
        => new()
        {
            ["id"] = Item.Id.Value,
            ["item_id"] = Item.ItemId,
            ["name"] = DisplayName,
            ["amount"] = Item.Amount,
            ["hue"] = (int)Item.Hue.Value,
            ["layer"] = Item.EquippedLayer?.ToString(),
            ["container"] = Item.ParentContainerId.Value,
            ["mobile"] = Item.EquippedMobileId.Value
        };
}
