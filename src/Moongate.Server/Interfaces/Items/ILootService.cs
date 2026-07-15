using Moongate.Persistence.Entities;

namespace Moongate.Server.Interfaces.Items;

/// <summary>Rolls a loot table into freshly created (unpersisted) items.</summary>
public interface ILootService
{
    /// <summary>Rolls the loot table and returns the created items, or empty when the table is unknown.</summary>
    IReadOnlyList<ItemEntity> Roll(string lootTableId);
}
