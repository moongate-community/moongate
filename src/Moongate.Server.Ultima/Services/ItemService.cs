using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Persistence.Interfaces;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Services.Internal;
using Moongate.Server.Ultima.Utils;
using Moongate.Ultima.Types;
using Moongate.Server.Ultima.Interfaces.Items;
using Serilog;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Keeps the live items by serial, the ones on the ground in the sector grid, the worn ones by wearer and the
///     contents by container, so what a mobile owns or a container holds is found without scanning the world. The
///     ground rules are
///     ModernUO's <c>DropToWorld</c>, simplified: a player reaches 2 tiles in line of sight, and a dropped item lands
///     on
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

    // The lists of one wearer or one container: the game loop is their only writer, and a default concurrent
    // dictionary costs five times the memory, for every backpack of the world.
    private const int InnerCapacity = 4;

    private readonly ConcurrentDictionary<Serial, ItemEntity> _items = new();
    private readonly ConcurrentDictionary<Serial, Serial?> _tombstones = new();

    // What lies inside a container that changed place: unchanged itself, but the database deletes it with the row the
    // container was under when that row goes in the same save (a dead NPC's backpack), so the save writes it again.
    private readonly ConcurrentDictionary<Serial, byte> _rewrites = new();
    private readonly ConcurrentDictionary<Serial, Serial> _released = new();
    private readonly ConcurrentDictionary<Serial, ConcurrentDictionary<Serial, ItemEntity>> _worn = new();

    // The items directly inside each container, and the container each item is filed under: taken from here when the
    // item moves, whatever the item itself says by then.
    private readonly ConcurrentDictionary<Serial, ConcurrentDictionary<Serial, ItemEntity>> _contents = new();
    private readonly ConcurrentDictionary<Serial, Serial> _filedIn = new();
    private readonly ILogger _logger = Log.ForContext<ItemService>();
    private readonly ISectorService _sectors;
    private readonly IMovementService _movement;
    private readonly ILineOfSightService _sight;
    private readonly IDataAccess<ItemEntity> _data;
    private readonly IGameLoopService _loop;
    private readonly IItemScriptService? _scripts;
    private readonly IItemDecayQueue? _decay;
    private readonly IItemTimerQueue? _timers;
    private readonly IInventoryMutationGuard? _inventory;

    public IReadOnlyCollection<ItemEntity> Items => _items.Values.ToArray();

    public ItemService(
        ISectorService sectors,
        IMovementService movement,
        ILineOfSightService sight,
        IDataAccess<ItemEntity> data,
        IGameLoopService loop,
        IItemScriptService? scripts = null,
        IItemDecayQueue? decay = null,
        IItemTimerQueue? timers = null,
        IInventoryMutationGuard? inventory = null
    )
    {
        _inventory = inventory;
        _timers = timers;
        _sectors = sectors;
        _movement = movement;
        _sight = sight;
        _data = data;
        _loop = loop;
        _scripts = scripts;
        _decay = decay;
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
        var batch = items.ToList();
        if (_inventory is not null)
        {
            var incoming = batch.ToDictionary(item => item.Id);
            foreach (var item in batch)
            {
                var seen = new HashSet<Serial>();
                var root = item;
                while (root.ContainerId is { } parent && incoming.TryGetValue(parent, out var next))
                {
                    if (!seen.Add(root.Id))
                    {
                        throw new InvalidOperationException("Cannot add cyclic inventory.");
                    }

                    root = next;
                }

                EnsureAllowed(root);
                if (_items.TryGetValue(item.Id, out var previous)) EnsureAllowed(previous);
            }
        }

        foreach (var item in batch)
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

            if (item.GroundLocation is not null)
            {
                _decay?.Track(item);
            }

            _timers?.Track(item);
        }
    }

    public IReadOnlyList<ItemEntity> AddLoaded(IEnumerable<ItemEntity> items)
    {
        var fresh = new List<ItemEntity>();
        var stale = new List<ItemEntity>();

        foreach (var item in items)
        {
            (_items.ContainsKey(item.Id) || _tombstones.ContainsKey(item.Id) ? stale : fresh).Add(item);
        }

        if (stale.Count > 0)
        {
            _logger.Warning(
                "{Count} loaded items are left out, live elsewhere or merged since their owner's last save: {Items}",
                stale.Count,
                stale.Select(item => item.Id).ToList()
            );
        }

        Add(fresh);

        return fresh;
    }

    public bool TryGet(Serial serial, [NotNullWhen(true)] out ItemEntity? item)
    {
        return _items.TryGetValue(serial, out item);
    }

    public void Remove(IEnumerable<Serial> serials)
    {
        var batch = serials.ToList();
        foreach (var serial in batch)
        {
            if (_items.TryGetValue(serial, out var item)) EnsureAllowed(item);
        }

        foreach (var serial in batch)
        {
            if (_items.TryRemove(serial, out var item))
            {
                _sectors.RemoveItem(item);
                Unindex(item);
                _decay?.Stop(item);
            }
        }
    }

    public IReadOnlyList<ItemEntity> GetContents(Serial container)
    {
        // An item changed outside the service may still be filed here: what no longer says so is left out. It is not
        // found where it went until it is added again: items move through the service.
        return _contents.TryGetValue(container, out var inside)
            ? inside.Values.Where(item => item.ContainerId == container).OrderBy(item => item.Id.Value).ToList()
            : [];
    }

    public Serial? GetOwner(ItemEntity item)
    {
        return GetWornRoot(item)?.MobileId;
    }

    public ItemEntity? GetWornRoot(ItemEntity item)
    {
        var visited = new HashSet<Serial>();
        var current = item;

        // Climbs the containers; a cycle or a container that is not live has no root.
        while (visited.Add(current.Id))
        {
            if (current.MobileId is not null)
            {
                return current;
            }

            if (current.ContainerId is not { } container || !_items.TryGetValue(container, out var parent))
            {
                return null;
            }

            current = parent;
        }

        return null;
    }

    public ItemEntity? GetGroundRoot(ItemEntity item)
    {
        var visited = new HashSet<Serial>();
        var current = item;

        // Climbs the containers; a cycle or a container that is not live has no root.
        while (visited.Add(current.Id))
        {
            if (current.GroundLocation is not null)
            {
                return current;
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

    public ItemEntity? GetWornAt(Serial mobile, LayerType layer)
    {
        if (!_worn.TryGetValue(mobile, out var worn))
        {
            return null;
        }

        foreach (var item in worn.Values)
        {
            if (item.Layer == layer)
            {
                return item;
            }
        }

        return null;
    }

    public IReadOnlyList<ItemEntity> GetOwnedBy(Serial mobile)
    {
        // What the mobile wears and, level by level, what lies inside it.
        var owned = GetWorn(mobile).ToList();

        for (var index = 0; index < owned.Count; index++)
        {
            owned.AddRange(GetContents(owned[index].Id));
        }

        return owned;
    }

    public void MoveToContainer(ItemEntity item, Serial container, Point2D position, int gridIndex = 0)
    {
        EnsureAllowed(item, container);
        var wearer = item.MobileId;
        _sectors.RemoveItem(item);
        Unindex(item);

        // Without the item itself: moved inside its own container it may keep its slot.
        var others = GetContents(container).Where(other => !ReferenceEquals(other, item));
        item.PutInContainer(container, position, ContainerSlotUtils.FirstFree(others, gridIndex));
        Index(item);
        _decay?.Stop(item);
        RewriteContents(item);
        WearerChanged(item, wearer);
    }

    public void PlaceOnGround(ItemEntity item, MapType map, Point3D location)
    {
        EnsureAllowed(item);
        var wearer = item.MobileId;
        _sectors.RemoveItem(item);
        Unindex(item);
        item.PlaceOnGround(map, location);
        _sectors.AddItem(item);
        _decay?.Restart(item);
        RewriteContents(item);
        WearerChanged(item, wearer);
    }

    public void Equip(ItemEntity item, Serial mobile, LayerType layer)
    {
        EnsureAllowed(item, mobile);
        var wearer = item.MobileId;
        _sectors.RemoveItem(item);
        Unindex(item);
        item.Equip(mobile, layer);
        Index(item);
        _decay?.Stop(item);
        RewriteContents(item);
        WearerChanged(item, wearer);
    }

    public bool CanReach(MobileEntity mobile, ItemEntity item)
    {
        return CanReach(mobile, item, GroundReach);
    }

    public bool CanReach(MobileEntity mobile, ItemEntity item, int range)
    {
        // Lying in the grid: a ground item someone holds keeps its location but is out of reach.
        return item.Map is { } map &&
               map == mobile.Map &&
               item.GroundLocation is { } spot &&
               IsNear(mobile.Location, spot.X, spot.Y, range) &&
               _sectors.ContainsItem(item) &&
               Sees(mobile, spot);
    }

    public bool TryDropOnGround(MobileEntity mobile, ItemEntity item, int x, int y)
    {
        if (_inventory?.Allows(item) == false || !IsNear(mobile.Location, x, y) ||
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
        EnsureAllowed(item);
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
        EnsureAllowed(item);
        _sectors.RemoveItem(item);
        _decay?.Stop(item);
    }

    public void Show(ItemEntity item)
    {
        EnsureAllowed(item);
        _sectors.AddItem(item);
        _decay?.Restart(item);
    }

    public ItemEntity Split(ItemEntity item, int amount, Serial serial)
    {
        EnsureAllowed(item);
        var rest = item.Snapshot();
        rest.Id = serial;

        // The timers stay with the part that keeps the serial: the rest is not a second item waiting for them.
        foreach (var key in rest.Props?.Keys
                     .Where(key => key.StartsWith(ItemTimerQueue.PropPrefix, StringComparison.Ordinal))
                     .ToList() ?? [])
        {
            rest.RemoveProp(key);
        }

        rest.Amount = item.Amount - amount;
        item.Amount = amount;
        _items[rest.Id] = rest;
        _sectors.AddItem(rest);
        Index(rest);

        // In a container the rest keeps the stack's grid slot and the lifted part takes a free one, as ServUO: when it
        // bounces back, the two stacks must not share a slot.
        if (item.ContainerId is { } container)
        {
            var others = GetContents(container).Where(other => !ReferenceEquals(other, item));
            item.GridIndex = ContainerSlotUtils.FirstFree(others);
        }

        // The rest stays where the stack lies, with the stack's decay time.
        if (rest.GroundLocation is not null)
        {
            _decay?.Track(rest);
        }

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

    public IReadOnlyCollection<Serial> CaptureRewrites()
    {
        return _rewrites.Keys.ToArray();
    }

    public void RewritesCommitted(IReadOnlyCollection<Serial> serials)
    {
        foreach (var serial in serials)
        {
            _rewrites.TryRemove(serial, out _);
        }
    }

    private void EnsureAllowed(ItemEntity item, Serial? destination = null)
    {
        if (_inventory?.Allows(item, destination) == false)
        {
            throw new InvalidOperationException("Inventory is reserved for attachment settlement.");
        }
    }

    // Everything inside the item, at any depth.
    private void RewriteContents(ItemEntity item)
    {
        var inside = new List<ItemEntity>(GetContents(item.Id));

        for (var index = 0; index < inside.Count; index++)
        {
            _rewrites[inside[index].Id] = 0;
            inside.AddRange(GetContents(inside[index].Id));
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

    private static bool IsNear(Point3D from, int x, int y, int range = GroundReach)
    {
        return Math.Abs(from.X - x) <= range && Math.Abs(from.Y - y) <= range;
    }

    private void AbsorbFor(ItemEntity item, Serial? owner)
    {
        EnsureAllowed(item);
        // A ground item a player released still has that player's row: the player's save must delete it, or the next
        // login would load it back into the backpack.
        if (_released.TryRemove(item.Id, out var releasedBy))
        {
            owner ??= releasedBy;
        }

        _tombstones[item.Id] = owner;
        _items.TryRemove(item.Id, out _);
        _sectors.RemoveItem(item);
        Unindex(item);
        _decay?.Stop(item);

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

    // Worn items by wearer and contents by container: a mobile shown to others (0x78) or a container opened must not
    // scan every item of the world.
    private void Index(ItemEntity item)
    {
        if (item.MobileId is { } wearer)
        {
            _worn.GetOrAdd(wearer, _ => new(1, InnerCapacity))[item.Id] = item;
        }

        if (item.ContainerId is { } container)
        {
            _contents.GetOrAdd(container, _ => new(1, InnerCapacity))[item.Id] = item;
            _filedIn[item.Id] = container;
        }
    }

    private void Unindex(ItemEntity item)
    {
        if (item.MobileId is { } wearer && _worn.TryGetValue(wearer, out var worn))
        {
            worn.TryRemove(item.Id, out _);
        }

        if (_filedIn.TryRemove(item.Id, out var container) && _contents.TryGetValue(container, out var inside))
        {
            inside.TryRemove(item.Id, out _);

            // An emptied container keeps no list: the world has many more items than containers in use.
            if (inside.IsEmpty)
            {
                _contents.TryRemove(new KeyValuePair<Serial, ConcurrentDictionary<Serial, ItemEntity>>(container, inside));
            }
        }
    }
}
