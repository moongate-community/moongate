using Moongate.Persistence.Entities;
using SquidStd.Scripting.Lua.Interfaces.Scripts;

namespace Moongate.Server.Scripting.Views;

/// <summary>Projects an <see cref="ItemEntity" /> into the Lua field table returned by <c>item.get</c>.</summary>
public sealed record ItemLuaView(ItemEntity Item) : ILuaTable
{
    public Dictionary<string, object?> ToDictionary()
        => new()
        {
            ["id"] = Item.Id.Value,
            ["item_id"] = Item.ItemId,
            ["name"] = Item.Name,
            ["amount"] = Item.Amount,
            ["hue"] = (int)Item.Hue.Value,
            ["layer"] = Item.EquippedLayer?.ToString(),
            ["container"] = Item.ParentContainerId.Value,
            ["mobile"] = Item.EquippedMobileId.Value
        };
}
