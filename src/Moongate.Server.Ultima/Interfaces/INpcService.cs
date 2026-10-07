using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     The NPCs of the world: loads them with their items at startup, spawns new ones from mobile templates and removes
///     them. They live in <see cref="IMobileService" /> and <see cref="IItemService" /> like the characters, and the world
///     save writes them.
/// </summary>
public interface INpcService : IMoongateStartupService
{
    /// <summary>
    ///     Spawns an NPC from template <paramref name="templateId" /> through <see cref="IMobileFactoryService" />, which
    ///     saves it with the <paramref name="props" /> given, then puts it and its items in the live world and shows it to the
    ///     players in range. Call it off the
    ///     game loop.
    /// </summary>
    Task<MobileEntity> SpawnAsync(
        string templateId,
        MapType map,
        Point3D location,
        IReadOnlyDictionary<string, object?>? props = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    ///     Takes a live NPC off the screens in range and out of the live world, with its items, and queues its row for
    ///     deletion by the next world save. False, and nothing happens, when the serial is not a live NPC. Call it off the
    ///     game loop.
    /// </summary>
    Task<bool> RemoveAsync(Serial serial, CancellationToken cancellationToken = default);

    /// <summary>
    ///     As <see cref="RemoveAsync" />, at once, for the code that already runs on the game loop, such as a death.
    ///     Call it on the game loop.
    /// </summary>
    bool Remove(Serial serial);
}
