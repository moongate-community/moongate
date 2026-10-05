using DryIoc;
using Moongate.Persistence.Extensions;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Services.Internal;

namespace Moongate.Server.Ultima.Extensions;

/// <summary>
///     Registers the bulletin board messages with persistence so the world save writes the live ones from
///     <see cref="IBulletinBoardService" />.
/// </summary>
public static class BulletinBoardContainerExtensions
{
    /// <summary>
    ///     Registers <see cref="BulletinMessageEntity" /> in the world database with the live messages as the save
    ///     source, each saved through <see cref="BulletinMessageEntity.Snapshot" />, and the removed messages as its
    ///     deletions.
    /// </summary>
    public static Container AddLiveBulletinMessages(this Container container)
    {
        return container.AddPersistenceWorld<BulletinMessageEntity>(
            () => container.Resolve<IBulletinBoardService>().Messages,
            message => message.Snapshot(),
            new LazyDeletionSource(() => container.Resolve<IBulletinBoardService>())
        );
    }
}
