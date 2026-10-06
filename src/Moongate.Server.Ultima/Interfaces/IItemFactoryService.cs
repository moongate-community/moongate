using Moongate.Core.Primitives;
using Moongate.Persistence.Interfaces;
using Moongate.Server.Ultima.Entities.World;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     Makes items from item templates and stores them. <see cref="Create" /> builds the item in memory; place it with
///     <see cref="ItemEntity.PlaceOnGround" />, <see cref="ItemEntity.PutInContainer" /> or <see cref="ItemEntity.Equip" />,
///     then save it, which gives it its serial.
/// </summary>
public interface IItemFactoryService
{
    /// <summary>
    ///     Builds an item from template <paramref name="templateId" />, with no serial and no location. Random template
    ///     values (hue, amount, rarity) are picked once, here; nothing else is copied from the template.
    /// </summary>
    /// <exception cref="KeyNotFoundException">
    ///     No template has that id.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     The amount is below 1.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///     The amount is above 1 and the template does not stack.
    /// </exception>
    ItemEntity Create(string templateId, int? amount = null, Hue? hue = null);

    /// <summary>
    ///     Saves one placed item in its own transaction.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    ///     The item has no location.
    /// </exception>
    Task SaveAsync(ItemEntity item, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Saves placed items in one transaction, in the order given: put a container before its contents.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    ///     An item has no location.
    /// </exception>
    Task SaveAsync(IReadOnlyList<ItemEntity> items, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Saves one placed item inside a transaction the caller opened on the world database.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    ///     The item has no location.
    /// </exception>
    Task SaveAsync(IPersistenceTransaction transaction, ItemEntity item, CancellationToken cancellationToken = default);
}
