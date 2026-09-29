using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Persistence.Interfaces;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Services.Internal;
using Moongate.Ultima.Types;
using Serilog;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Keeps the live items by serial, the ones on the ground in the sector grid, and the worn ones by wearer. Contents
///     and owners are found by scanning, which only a character's leave and its containers need. The ground rules are
///     ModernUO's <c>DropToWorld</c>, simplified: a player reaches 2 tiles in line of sight, and a dropped item lands on
///     the highest surface up to 16 above the player's feet, without stacking on other ground items. At startup it
///     loads the items lying on the ground with their contents.
/// </summary>
public sealed class ItemService : IItemService, IMoongateStartupService
{
    public const int GroundReach = 2;
    public const string EquipFunction = "on_equip";
    public const string UnequipFunction = "on_unequip";
    private const int DropCeiling = 16;
    private const int EyeHeight = 14;

    private readonly ConcurrentDictionary<Serial, ItemEntity> _items = new();
    private readonly ConcurrentDictionary<Serial, Serial?> _tombstones = new();
    private readonly ConcurrentDictionary<Serial, Serial> _released = new();
    private readonly ConcurrentDictionary<Serial, ConcurrentDictionary<Serial, ItemEntity>> _worn = new();
    private readonly ILogger _logger = Log.ForContext<ItemService>();
    private readonly ISectorService _sectors;
    private readonly IMovementService _movement;
    private readonly ILineOfSightService _sight;
    private readonly IDataAccess<ItemEntity> _data;
    private readonly IGameLoopService _loop;
    private readonly IItemScriptService? _scripts;

    public IReadOnlyCollection<ItemEntity> Items => _items.Values.ToArray();

    public ItemService(
        ISectorService sectors,
        IMovementService movement,
        ILineOfSightService sight,
        IDataAccess<ItemEntity> data,
        IGameLoopService loop,
        IItemScriptService? scripts = null
    )
    {
        _sectors = sectors;
        _movement = movement;
        _sight = sight;
        _data = data;
        _loop = loop;
        _scripts = scripts;
    }

    public async Task StartAsync()
    {
        // The items lying on the ground and everything inside them; the characters' items come with them at login.
        var roots = await _data.QueryAsync(item => item.Map != null);
        var loaded = await ItemContentsLoader.LoadAsync(_data, roots);

        // The live items and the sector grid change only on the game loop.
        var work = new LoopActionWorkItem(() => Add(loaded));
        await _loop.PostAsync(work);
        await work.Completion;
        _logger.Information("Loaded {Roots} items on the ground, {Total} with their contents", roots.Count, loaded.Count);
    }

    public Task StopAsync()
    {
        return Task.CompletedTask;
    }

    public void Add(IEnumerable<ItemEntity> items)
    {
        foreach (var item in items)
        {
            if (_items.TryGetValue(item.Id, out var previous))
            {
                _sectors.RemoveItem(previous);
                Unindex(previous);
            }

            _items[item.Id] = item;
            _tombstones.TryRemove(item.Id, out _);
            _sectors.AddItem(item);
            Index(item);
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
                Unindex(item);
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

    public IReadOnlyList<ItemEntity> GetWorn(Serial mobile)
    {
        return _worn.TryGetValue(mobile, out var worn) ? worn.Values.ToList() : [];
    }

    public IReadOnlyList<ItemEntity> GetOwnedBy(Serial mobile)
    {
        return _items.Values.Where(item => GetOwner(item) == mobile).ToList();
    }

    public void MoveToContainer(ItemEntity item, Serial container, Point2D position)
    {
        var wearer = item.MobileId;
        _sectors.RemoveItem(item);
        Unindex(item);
        item.PutInContainer(container, position);
        WearerChanged(item, wearer);
    }

    public void PlaceOnGround(ItemEntity item, MapType map, Point3D location)
    {
        var wearer = item.MobileId;
        _sectors.RemoveItem(item);
        Unindex(item);
        item.PlaceOnGround(map, location);
        _sectors.AddItem(item);
        WearerChanged(item, wearer);
    }

    public void Equip(ItemEntity item, Serial mobile, LayerType layer)
    {
        var wearer = item.MobileId;
        _sectors.RemoveItem(item);
        Unindex(item);
        item.Equip(mobile, layer);
        Index(item);
        WearerChanged(item, wearer);
    }

    public bool CanReach(MobileEntity mobile, ItemEntity item)
    {
        // Lying in the grid: a ground item someone holds keeps its location but is out of reach.
        return item.Map is { } map &&
               map == mobile.Map &&
               item.GroundLocation is { } spot &&
               IsNear(mobile.Location, spot.X, spot.Y) &&
               _sectors.ContainsItem(item) &&
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

    public bool IsLyingOnGround(ItemEntity item)
    {
        return _sectors.ContainsItem(item);
    }

    public void Release(ItemEntity item, Serial owner)
    {
        _released[item.Id] = owner;
    }

    public IReadOnlyList<ItemEntity> TakeReleasedOf(Serial owner)
    {
        var taken = new List<ItemEntity>();

        foreach (var pair in _released.Where(pair => pair.Value == owner).ToList())
        {
            if (_released.TryRemove(pair.Key, out _) && _items.TryGetValue(pair.Key, out var item))
            {
                taken.Add(item);
            }
        }

        return taken;
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
        Index(rest);

        return rest;
    }

    public void Absorb(ItemEntity item)
    {
        AbsorbFor(item, GetOwner(item));
    }

    public void Absorb(ItemEntity item, Serial owner)
    {
        AbsorbFor(item, owner);
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

    private void AbsorbFor(ItemEntity item, Serial? owner)
    {
        _tombstones[item.Id] = owner;
        _items.TryRemove(item.Id, out _);
        _sectors.RemoveItem(item);
        Unindex(item);

        // A worn item merged into a stack leaves its layer for good.
        if (item.MobileId is { } wearer)
        {
            _scripts?.Queue(item, UnequipFunction, (long)wearer.Value);
        }
    }

    // The item's script hears a wearer change: on_unequip for the one it left, then on_equip for the one it went onto.
    // Queued, so the caller's packets go out first: a script that deletes the item must not leave it drawn. Items loaded
    // or spawned already dressed never pass here.
    private void WearerChanged(ItemEntity item, Serial? before)
    {
        if (_scripts is null || before == item.MobileId)
        {
            return;
        }

        if (before is { } left)
        {
            _scripts.Queue(item, UnequipFunction, (long)left.Value);
        }

        if (item.MobileId is { } wearer)
        {
            _scripts.Queue(item, EquipFunction, (long)wearer.Value);
        }
    }

    // Worn items by wearer: a mobile shown to others (0x78) must not scan every item of the world.
    private void Index(ItemEntity item)
    {
        if (item.MobileId is { } wearer)
        {
            _worn.GetOrAdd(wearer, _ => new())[item.Id] = item;
        }
    }

    private void Unindex(ItemEntity item)
    {
        if (item.MobileId is { } wearer && _worn.TryGetValue(wearer, out var worn))
        {
            worn.TryRemove(item.Id, out _);
        }
    }
}
