using DryIoc;
using Moongate.Persistence.Extensions;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Server.Ultima.Extensions;

/// <summary>
///     Registers the world mobiles with persistence so the world save writes the live ones from
///     <see cref="IMobileService" />.
/// </summary>
public static class WorldMobilesContainerExtensions
{
    /// <summary>
    ///     Registers <see cref="MobileEntity" /> in the world database with the live mobiles as the save source, each saved
    ///     through <see cref="MobileEntity.Snapshot" />.
    /// </summary>
    public static Container AddLiveWorldMobiles(this Container container)
    {
        return container.AddPersistenceWorld<MobileEntity>(
            () => container.Resolve<IMobileService>().Mobiles,
            mobile => mobile.Snapshot()
        );
    }
}
