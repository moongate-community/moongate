using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Counts the items of a container against its limit, as <see cref="WeightService" /> does for the stones: the
///     container itself and every container it is inside.
/// </summary>
public sealed class ContainerCapacityService : IContainerCapacityService
{
    private readonly IItemService _items;
    private readonly IItemTemplateService _templates;
    private readonly BankConfig _bank;

    public ContainerCapacityService(IItemService items, IItemTemplateService templates, BankConfig bank)
    {
        _items = items;
        _templates = templates;
        _bank = bank;
    }

    public int? MaximumOf(ItemEntity container)
    {
        // A bank box takes the setting, whatever its template says; 0 there is no limit.
        if (container.Layer == LayerType.Bank)
        {
            return _bank.MaxItems > 0 ? _bank.MaxItems : null;
        }

        return _templates.TryGet(container.TemplateId, out var template) && template.MaxItems is > 0 and var maximum
            ? maximum
            : null;
    }

    public int CountIn(ItemEntity container)
    {
        return CountIn(container, []);
    }

    public bool HasRoom(ItemEntity container, ItemEntity item)
    {
        return HasRoom(container, 1 + CountIn(item), Around(item));
    }

    public bool HasRoomFor(ItemEntity container, int items)
    {
        return items <= 0 || HasRoom(container, items, []);
    }

    private bool HasRoom(ItemEntity container, int added, HashSet<Serial> around)
    {
        var visited = new HashSet<Serial>();

        for (var current = container; current is not null && visited.Add(current.Id); current = Parent(current))
        {
            // A container the item is already in gets no fuller: its items move around.
            if (around.Contains(current.Id))
            {
                continue;
            }

            if (MaximumOf(current) is { } maximum && CountIn(current) + added > maximum)
            {
                return false;
            }
        }

        return true;
    }

    // The set stops a loop of containers.
    private int CountIn(ItemEntity container, HashSet<Serial> visited)
    {
        if (!visited.Add(container.Id))
        {
            return 0;
        }

        var count = 0;

        foreach (var content in _items.GetContents(container.Id))
        {
            count += 1 + CountIn(content, visited);
        }

        return count;
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
}
