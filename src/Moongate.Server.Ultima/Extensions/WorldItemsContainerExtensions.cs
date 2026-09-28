using DryIoc;
using Moongate.Persistence.Extensions;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Server.Ultima.Extensions;

/// <summary>
///     Registers the world items with persistence so the world save writes the live ones from
///     <see cref="IItemService" />.
/// </summary>
public static class WorldItemsContainerExtensions
{
    /// <summary>
    ///     Registers <see cref="ItemEntity" /> in the world database with the live items as the save source, each saved
    ///     through <see cref="ItemEntity.Snapshot" />. Register it after the mobiles: worn items point at their rows.
    /// </summary>
    public static Container AddLiveWorldItems(this Container container)
    {
        return container.AddPersistenceWorld<ItemEntity>(
            () => container.Resolve<IItemService>().Items,
            item => item.Snapshot()
        );
    }
}
