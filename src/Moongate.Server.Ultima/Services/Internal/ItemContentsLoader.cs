using Moongate.Core.Primitives;
using Moongate.Persistence.Interfaces;
using Moongate.Server.Ultima.Entities.World;

namespace Moongate.Server.Ultima.Services.Internal;

/// <summary>
///     Reads what a set of items holds, one level of containers at a time, as a character's items are read at login.
/// </summary>
public static class ItemContentsLoader
{
    /// <summary>
    ///     Gets <paramref name="roots" /> followed by everything inside them at any depth; the visited set stops a cycle.
    /// </summary>
    public static async Task<List<ItemEntity>> LoadAsync(IDataAccess<ItemEntity> data, IReadOnlyList<ItemEntity> roots)
    {
        var loaded = new List<ItemEntity>(roots);
        var visited = roots.Select(item => item.Id).ToHashSet();
        var containers = visited.Select(serial => (Serial?)serial).ToList();

        while (containers.Count > 0)
        {
            var level = await data.QueryAsync(item => containers.Contains(item.ContainerId));
            var fresh = level.Where(item => visited.Add(item.Id)).ToList();
            loaded.AddRange(fresh);
            containers = fresh.Select(item => (Serial?)item.Id).ToList();
        }

        return loaded;
    }
}
