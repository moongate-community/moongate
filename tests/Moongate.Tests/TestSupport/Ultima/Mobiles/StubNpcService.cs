using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Ultima.Types;

namespace Moongate.Tests.TestSupport.Ultima.Mobiles;

/// <summary>
///     Records spawns and removals: a spawn returns <see cref="Spawned" /> (or throws <see cref="SpawnFailure" />), a
///     removal returns <see cref="Removes" />.
/// </summary>
public sealed class StubNpcService : INpcService
{
    public MobileEntity Spawned { get; set; } = new() { Id = new(0x00000100), Name = "Orc", TemplateId = "orc" };

    public Exception? SpawnFailure { get; set; }

    public bool Removes { get; set; } = true;

    public List<(string TemplateId, MapType Map, Point3D Location)> Spawns { get; } = [];

    public List<Serial> Removals { get; } = [];

    public Task StartAsync()
    {
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        return Task.CompletedTask;
    }

    public Task<MobileEntity> SpawnAsync(
        string templateId,
        MapType map,
        Point3D location,
        CancellationToken cancellationToken = default
    )
    {
        Spawns.Add((templateId, map, location));
        Spawned.Map = map;
        Spawned.Location = location;

        return SpawnFailure is null ? Task.FromResult(Spawned) : Task.FromException<MobileEntity>(SpawnFailure);
    }

    public Task<bool> RemoveAsync(Serial serial, CancellationToken cancellationToken = default)
    {
        Removals.Add(serial);

        return Task.FromResult(Removes);
    }
}
