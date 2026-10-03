using DryIoc;
using Moongate.Persistence.Extensions;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Server.Ultima.Extensions;

/// <summary>
///     Registers the world state with persistence so the world save writes the live one from
///     <see cref="IWorldPropsService" />.
/// </summary>
public static class WorldStateContainerExtensions
{
    /// <summary>
    ///     Registers <see cref="WorldStateEntity" /> in the world database with the live state as the save source, saved
    ///     through <see cref="WorldStateEntity.Snapshot" />.
    /// </summary>
    public static Container AddLiveWorldState(this Container container)
    {
        return container.AddPersistenceWorld<WorldStateEntity>(
            () => [container.Resolve<IWorldPropsService>().State],
            state => state.Snapshot()
        );
    }
}
