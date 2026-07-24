using Moongate.Server.Abstractions.Interfaces.Items;
using SquidStd.Scripting.Lua.Attributes.Scripts;

namespace Moongate.Server.Scripting;

/// <summary>
/// Exposes loot rolling to Lua. <c>loot.roll</c> creates the rolled items (floating, unplaced) and
/// returns their serials; the script places them (for example with <c>item.add_to_container</c>).
/// Item creation is guarded by <see cref="IItemService" />'s own <c>ILoopAffinity</c> check.
/// </summary>
[ScriptModule("loot", "Roll loot tables into items.")]
public sealed class LootModule
{
    private readonly ILootService _loot;
    private readonly IItemService _items;

    public LootModule(ILootService loot, IItemService items)
    {
        _loot = loot;
        _items = items;
    }

    [ScriptFunction("roll", "Rolls a loot table; creates the items and returns their serials.")]
    public List<uint> Roll(string lootTableId)
    {
        var serials = new List<uint>();

        foreach (var item in _loot.Roll(lootTableId))
        {
            serials.Add(_items.Create(item).Value);
        }

        return serials;
    }
}
