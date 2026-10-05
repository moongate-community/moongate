using Moongate.Core.Primitives;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Items;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Extensions;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Counts weights from the live items: nothing is kept, so a total is always what the items say now.
/// </summary>
public sealed class WeightService : IWeightService
{
    private const int BaseCarried = 40;
    private const double CarriedPerStrength = 3.5;

    private readonly IItemService _items;
    private readonly IItemTemplateService _templates;
    private readonly ITileDataService _tiles;
    private readonly ISessionService? _sessions;

    public WeightService(
        IItemService items,
        IItemTemplateService templates,
        ITileDataService tiles,
        ISessionService? sessions = null
    )
    {
        _items = items;
        _templates = templates;
        _tiles = tiles;
        _sessions = sessions;
    }

    public int Of(ItemEntity item)
    {
        return Of(item, null, []);
    }

    public int Carried(MobileEntity mobile)
    {
        var total = _items.GetWorn(mobile.Id).Where(worn => worn.Layer != LayerType.Bank).Sum(Of);

        // What its player lifted from the ground, from a chest or out of the bank is in its hand, as in ModernUO;
        // what it lifted from its own pack is still counted there.
        if (_sessions is not null &&
            _sessions.TryGetByCharacterId(mobile.Id, out var session) &&
            session.Get(ItemSessionKeys.Held) is { } held &&
            _items.TryGet(held.Item, out var inHand) &&
            (_items.GetOwner(inHand) != mobile.Id || _items.GetWornRoot(inHand)?.Layer == LayerType.Bank))
        {
            total += Of(inHand);
        }

        return total;
    }

    public int MaxCarried(MobileEntity mobile)
    {
        return BaseCarried + (int)(CarriedPerStrength * mobile.Strength);
    }

    public bool Holds(ItemEntity container, ItemEntity item)
    {
        var added = Of(item);
        var around = Around(item);
        var visited = new HashSet<Serial>();

        for (var current = container; current is not null && visited.Add(current.Id); current = Parent(current))
        {
            if (current.Layer == LayerType.Bank)
            {
                return true;
            }

            // A container the item is already in gets no heavier: over its limit or not, its items move around.
            if (around.Contains(current.Id))
            {
                continue;
            }

            var maximum = MaximumOf(current);

            if (maximum > 0 && Of(current, item, []) - PileWeight(current) + added > maximum)
            {
                return false;
            }
        }

        return true;
    }

    public bool Holds(ItemEntity container, IReadOnlyList<ItemEntity> additions)
    {
        var added = additions.Sum(item => (long)Of(item));
        var visited = new HashSet<Serial>();
        for (var current = container; current is not null; current = Parent(current))
        {
            if (!visited.Add(current.Id))
            {
                return false;
            }
            if (current.Layer == LayerType.Bank)
            {
                return true;
            }
            var maximum = MaximumOf(current);
            if (maximum > 0 && (long)Of(current) - PileWeight(current) + added > maximum)
            {
                return false;
            }
            if (current.ContainerId is { } parent && !_items.TryGet(parent, out _))
            {
                return false;
            }
        }
        return true;
    }

    // The containers the item is in, at any depth.
    private HashSet<Serial> Around(ItemEntity item)
    {
        var around = new HashSet<Serial>();

        for (var current = Parent(item); current is not null && around.Add(current.Id); current = Parent(current))
        {
        }

        return around;
    }

    private ItemEntity? Parent(ItemEntity item)
    {
        return item.ContainerId is { } parent && _items.TryGet(parent, out var container) ? container : null;
    }

    private int MaximumOf(ItemEntity container)
    {
        return _templates.TryGet(container.TemplateId, out var template) && template.MaxWeight is { } maximum
            ? maximum
            : IWeightService.DefaultContainerMaximum;
    }

    // The item with its contents, leaving out one item wherever it is inside; the set stops a loop of containers.
    private int Of(ItemEntity item, ItemEntity? except, HashSet<Serial> visited)
    {
        if (!visited.Add(item.Id))
        {
            return 0;
        }

        var total = PileWeight(item);

        foreach (var inside in _items.GetContents(item.Id))
        {
            if (except is null || inside.Id != except.Id)
            {
                total += Of(inside, except, visited);
            }
        }

        return total;
    }

    private int PileWeight(ItemEntity item)
    {
        decimal unit;

        if (_templates.TryGet(item.TemplateId, out var template) && template.Weight is { } own)
        {
            unit = own;
        }
        else
        {
            // The tiledata weight of the graphic the item has now; 255 there is "cannot be lifted", not a weight.
            unit = _tiles.TryGetItem(item.ItemId, out var tile) &&
                   tile.Weight < ItemTemplateExtensions.TiledataWeightCannotLift
                ? tile.Weight
                : 0;
        }

        return (int)Math.Ceiling(unit * Math.Max(item.Amount, 1));
    }
}
