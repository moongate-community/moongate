using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Ultima.Types;
using Serilog;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Keeps the live items by serial, and the ones on the ground in the sector grid. Contents and owners are found by
///     scanning: a player holds few items. The ground rules are ModernUO's <c>DropToWorld</c>, simplified: a player
///     reaches 2 tiles in line of sight, and a dropped item lands on the highest surface up to 16 above the player's
///     feet, without stacking on other ground items.
/// </summary>
public sealed class ItemService : IItemService
{
    public const int GroundReach = 2;
    private const int DropCeiling = 16;
    private const int EyeHeight = 14;

    private readonly ConcurrentDictionary<Serial, ItemEntity> _items = new();
    private readonly ConcurrentDictionary<Serial, Serial?> _tombstones = new();
    private readonly ILogger _logger = Log.ForContext<ItemService>();
    private readonly ISectorService _sectors;
    private readonly IMovementService _movement;
    private readonly ILineOfSightService _sight;

    public IReadOnlyCollection<ItemEntity> Items => _items.Values.ToArray();

    public ItemService(ISectorService sectors, IMovementService movement, ILineOfSightService sight)
    {
        _sectors = sectors;
        _movement = movement;
        _sight = sight;
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

    public bool CanReach(MobileEntity mobile, ItemEntity item)
    {
        // Lying in the grid: a ground item someone holds keeps its location but is out of reach.
        return item.Map is { } map &&
               map == mobile.Map &&
               item.GroundLocation is { } spot &&
               IsNear(mobile.Location, spot.X, spot.Y) &&
               _sectors.GetItemsInRange(map, spot, 0).Any(other => other.Id == item.Id) &&
               Sees(mobile, spot);
    }

    public bool TryDropOnGround(MobileEntity mobile, ItemEntity item, int x, int y)
    {
        if (!IsNear(mobile.Location, x, y) ||
            !_movement.TryGetDropZ(mobile.Map, x, y, mobile.Location.Z + DropCeiling, out var z))
        {
            return false;
        }

        var spot = new Point3D(x, y, z);

        if (!Sees(mobile, spot))
        {
            return false;
        }

        PlaceOnGround(item, mobile.Map, spot);

        return true;
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

    // From the eyes to just above the spot, as ModernUO checks a drop.
    private bool Sees(MobileEntity mobile, Point3D spot)
    {
        var eye = new Point3D(mobile.Location.X, mobile.Location.Y, mobile.Location.Z + EyeHeight);

        try
        {
            return _sight.HasLineOfSight(mobile.Map, eye, new Point3D(spot.X, spot.Y, spot.Z + 1));
        }
        catch (KeyNotFoundException exception)
        {
            _logger.Warning(exception, "{Mobile} cannot see the ground: its map is not loaded", mobile);

            return false;
        }
    }

    private static bool IsNear(Point3D from, int x, int y)
    {
        return Math.Abs(from.X - x) <= GroundReach && Math.Abs(from.Y - y) <= GroundReach;
    }
}
