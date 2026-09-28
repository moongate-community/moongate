using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Keeps the live items by serial. Contents and owners are found by scanning: a player holds few items.
/// </summary>
public sealed class ItemService : IItemService
{
    private readonly ConcurrentDictionary<Serial, ItemEntity> _items = new();

    public IReadOnlyCollection<ItemEntity> Items => _items.Values.ToArray();

    public void Add(IEnumerable<ItemEntity> items)
    {
        foreach (var item in items)
        {
            _items[item.Id] = item;
        }
    }

    public bool TryGet(Serial serial, [NotNullWhen(true)] out ItemEntity? item)
    {
        return _items.TryGetValue(serial, out item);
    }

    public void Remove(IEnumerable<Serial> serials)
    {
        foreach (var serial in serials)
        {
            _items.TryRemove(serial, out _);
        }
    }

    public IReadOnlyList<ItemEntity> GetContents(Serial container)
    {
        return _items.Values.Where(item => item.ContainerId == container).OrderBy(item => item.Id.Value).ToList();
    }

    public Serial? GetOwner(ItemEntity item)
    {
        var visited = new HashSet<Serial>();
        var current = item;

        // Climbs the containers; a cycle or a container that is not live has no owner.
        while (visited.Add(current.Id))
        {
            if (current.MobileId is { } wearer)
            {
                return wearer;
            }

            if (current.ContainerId is not { } container || !_items.TryGetValue(container, out var parent))
            {
                return null;
            }

            current = parent;
        }

        return null;
    }

    public IReadOnlyList<ItemEntity> GetOwnedBy(Serial mobile)
    {
        return _items.Values.Where(item => GetOwner(item) == mobile).ToList();
    }

    public void MoveToContainer(ItemEntity item, Serial container, Point2D position)
    {
        item.PutInContainer(container, position);
    }
}
