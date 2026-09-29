using DryIoc;
using Moongate.Persistence.Extensions;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Services.Internal;

namespace Moongate.Server.Ultima.Extensions;

/// <summary>
///     Registers the world items with persistence so the world save writes the live ones from
///     <see cref="IItemService" />.
/// </summary>
public static class WorldItemsContainerExtensions
{
    /// <summary>
    ///     Registers <see cref="ItemEntity" /> in the world database with the live items as the save source, each saved
    ///     through <see cref="ItemEntity.Snapshot" />, and the items absorbed into other stacks as its deletions. Register
    ///     it after the mobiles, whose rows worn items point at, and after <see cref="IItemService" />.
    /// </summary>
    public static Container AddLiveWorldItems(this Container container)
    {
        return container.AddPersistenceWorld<ItemEntity>(
            // Unworn items first: a layer taken off one item is free before another is written onto it, which the
            // unique (mobile, layer) index needs.
            () => container.Resolve<IItemService>().Items.OrderBy(item => item.MobileId is not null),
            item => item.Snapshot(),
            new LazyDeletionSource(() => container.Resolve<IItemService>())
        );
    }
}
