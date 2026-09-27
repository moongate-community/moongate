using Moongate.Core.Geometry;
using Moongate.Server.Ultima.Data.Mobiles;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     Makes mobiles from mobile templates. <see cref="SpawnAsync" /> puts a new, dressed NPC in the world in one step,
///     as UOX3's <c>CreateNPCxyz</c>; <see cref="Create" /> only builds it in memory.
/// </summary>
public interface IMobileFactoryService
{
    /// <summary>
    ///     Builds a mobile from template <paramref name="templateId" />, with no serial, no place and no equipment. Every
    ///     random template value (gender, name, looks, stats, skills) is rolled once, here.
    /// </summary>
    /// <exception cref="KeyNotFoundException">No template has that id.</exception>
    MobileEntity Create(string templateId);

    /// <summary>
    ///     Creates a mobile from template <paramref name="templateId" />, puts it at <paramref name="location" /> on
    ///     <paramref name="map" />, and saves it with the equipment of its template in one transaction. Publishes
    ///     <c>MobileBeforeSpawnEvent</c> before saving (a handler may change the mobile), then
    ///     <c>MobileMovedToWorldEvent</c> and <c>MobileAfterSpawnEvent</c> after the commit. The event bus logs a handler's
    ///     exception and goes on, so a handler cannot stop or undo a spawn.
    /// </summary>
    /// <exception cref="KeyNotFoundException">No template has that id.</exception>
    /// <exception cref="InvalidDataException">The template resolves to no body and no race.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The location is outside the map.</exception>
    Task<SpawnedMobile> SpawnAsync(
        string templateId,
        MapType map,
        Point3D location,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    ///     Saves a mobile that already has its serial, in its own transaction.
    /// </summary>
    /// <exception cref="InvalidOperationException">The mobile has no serial yet: spawn it first.</exception>
    Task SaveAsync(MobileEntity mobile, CancellationToken cancellationToken = default);
}
