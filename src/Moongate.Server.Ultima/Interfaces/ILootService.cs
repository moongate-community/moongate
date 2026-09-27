using System.Diagnostics.CodeAnalysis;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Entities.World;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     Rolls the loot tables of <c>templates/loots/</c>.
/// </summary>
public interface ILootService
{
    /// <summary>
    ///     Gets the table with id <paramref name="id" />, matching case.
    /// </summary>
    bool TryGet(string id, [NotNullWhen(true)] out LootTemplate? table);

    /// <summary>
    ///     Gets the table with id <paramref name="id" />, matching case.
    /// </summary>
    /// <exception cref="KeyNotFoundException">No table has that id.</exception>
    LootTemplate Get(string id);

    /// <summary>
    ///     Picks one entry of the table by weight and returns what it gives, built through the item factory with no
    ///     serial and no location: nothing for a blank entry, one pile for a stackable item, that many items
    ///     otherwise, and the roll of a nested table.
    /// </summary>
    /// <exception cref="KeyNotFoundException">No table has that id.</exception>
    IReadOnlyList<ItemEntity> Roll(string lootTemplateId);
}
