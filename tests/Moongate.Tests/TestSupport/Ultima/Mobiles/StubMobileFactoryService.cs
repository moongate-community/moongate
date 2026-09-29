using Moongate.Core.Geometry;
using Moongate.Server.Ultima.Data.Mobiles;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Ultima.Types;

namespace Moongate.Tests.TestSupport.Ultima.Mobiles;

/// <summary>
///     Spawns <see cref="Spawned" /> at the requested place, without templates or a database, and records the calls.
/// </summary>
public sealed class StubMobileFactoryService : IMobileFactoryService
{
    public required SpawnedMobile Spawned { get; init; }

    public List<(string TemplateId, MapType Map, Point3D Location)> Spawns { get; } = [];

    public MobileEntity Create(string templateId)
    {
        return Spawned.Mobile;
    }

    public Task<SpawnedMobile> SpawnAsync(
        string templateId,
        MapType map,
        Point3D location,
        CancellationToken cancellationToken = default
    )
    {
        Spawns.Add((templateId, map, location));
        Spawned.Mobile.Map = map;
        Spawned.Mobile.Location = location;

        return Task.FromResult(Spawned);
    }

    public Task SaveAsync(MobileEntity mobile, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
