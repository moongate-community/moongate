using Moongate.Core.Geometry;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     Puts new items on the ground of the live world, as a spawn region does with a treasure chest: a container comes
///     with the gold and the loot of its template inside.
/// </summary>
public interface IItemSpawnService
{
    /// <summary>
    ///     Makes an item from template <paramref name="templateId" /> on the ground at <paramref name="location" />
    ///     with the <paramref name="props" /> given, fills it with the gold and the loot its template names, saves it
    ///     and its contents, then puts them in the live world and shows the item to the players in range. Call it off
    ///     the game loop.
    /// </summary>
    /// <exception cref="KeyNotFoundException">
    ///     No template has that id.
    /// </exception>
    Task<ItemEntity> SpawnAsync(
        string templateId,
        MapType map,
        Point3D location,
        IReadOnlyDictionary<string, object?>? props = null,
        CancellationToken cancellationToken = default
    );
}
