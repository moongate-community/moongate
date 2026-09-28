using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Keeps the live items by serial, and the ones on the ground in the sector grid. Contents and owners are found by
///     scanning: a player holds few items.
/// </summary>
public sealed class ItemService : IItemService
{
    private readonly ConcurrentDictionary<Serial, ItemEntity> _items = new();
    private readonly ConcurrentDictionary<Serial, Serial?> _tombstones = new();
    private readonly ISectorService _sectors;

    public IReadOnlyCollection<ItemEntity> Items => _items.Values.ToArray();

    public ItemService(ISectorService sectors)
    {
        _sectors = sectors;
    }

    public void Add(IEnumerable<ItemEntity> items)
    {
        foreach (var item in items)
        {
            if (_items.TryGetValue(item.Id, out var previous))
            {
                _sectors.RemoveItem(previous);
            }

            _items[item.Id] = item;
            _tombstones.TryRemove(item.Id, out _);
            _sectors.AddItem(item);
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
            if (_items.TryRemove(serial, out var item))
            {
                _sectors.RemoveItem(item);
            }
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
        _sectors.RemoveItem(item);
        item.PutInContainer(container, position);
    }

    public void PlaceOnGround(ItemEntity item, MapType map, Point3D location)
    {
        _sectors.RemoveItem(item);
        item.PlaceOnGround(map, location);
        _sectors.AddItem(item);
    }

    public void Hide(ItemEntity item)
    {
        _sectors.RemoveItem(item);
    }

    public void Show(ItemEntity item)
    {
        _sectors.AddItem(item);
    }

    public ItemEntity Split(ItemEntity item, int amount, Serial serial)
    {
        var rest = item.Snapshot();
        rest.Id = serial;
        rest.Amount = item.Amount - amount;
        item.Amount = amount;
        _items[rest.Id] = rest;
        _sectors.AddItem(rest);

        return rest;
    }

    public void Absorb(ItemEntity item)
    {
        _tombstones[item.Id] = GetOwner(item);
        _items.TryRemove(item.Id, out _);
        _sectors.RemoveItem(item);
    }

    public IReadOnlyCollection<Serial> TombstonesOf(Serial owner)
    {
        return _tombstones.Where(pair => pair.Value == owner).Select(pair => pair.Key).ToArray();
    }

    public IReadOnlyCollection<Serial> TakeTombstonesOf(Serial owner)
    {
        var taken = new List<Serial>();

        foreach (var serial in TombstonesOf(owner))
        {
            if (_tombstones.TryRemove(serial, out _))
            {
                taken.Add(serial);
            }
        }

        return taken;
    }

    public IReadOnlyCollection<Serial> Capture()
    {
        return _tombstones.Keys.ToArray();
    }

    public void Committed(IReadOnlyCollection<Serial> serials)
    {
        foreach (var serial in serials)
        {
            _tombstones.TryRemove(serial, out _);
        }
    }
}
