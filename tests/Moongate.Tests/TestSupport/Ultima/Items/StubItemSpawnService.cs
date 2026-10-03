using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Ultima.Types;

namespace Moongate.Tests.TestSupport.Ultima.Items;

/// <summary>
///     Records the item spawns and gives each a new item on the ground with the props asked for, put into
///     <see cref="Items" /> when one is given, as the real service puts it in the live world.
/// </summary>
public sealed class StubItemSpawnService : IItemSpawnService
{
    private uint _next = 0x40001000;

    public List<(string TemplateId, MapType Map, Point3D Location)> Spawns { get; } = [];

    public IItemService? Items { get; set; }

    public Exception? SpawnFailure { get; set; }

    public Task<ItemEntity> SpawnAsync(
        string templateId,
        MapType map,
        Point3D location,
        IReadOnlyDictionary<string, object?>? props = null,
        CancellationToken cancellationToken = default
    )
    {
        Spawns.Add((templateId, map, location));

        if (SpawnFailure is not null)
        {
            throw SpawnFailure;
        }

        var item = new ItemEntity { Id = new Serial(_next++), TemplateId = templateId, ItemId = 0x0E41, Amount = 1 };
        item.PlaceOnGround(map, location);

        foreach (var (key, value) in props ?? new Dictionary<string, object?>())
        {
            item.SetProp(key, value);
        }

        Items?.Add([item]);

        return Task.FromResult(item);
    }
}
