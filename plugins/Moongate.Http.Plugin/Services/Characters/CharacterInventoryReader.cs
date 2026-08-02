using Moongate.Core.Primitives;
using Moongate.Http.Plugin.Data.Api.Characters;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Interfaces.Items;

namespace Moongate.Http.Plugin.Services.Characters;

/// <summary>
/// Turns a character's worn items and backpack into the API's item tree.
/// The walk is recursive over a containment graph nothing validates, and it runs on a request thread —
/// so it carries two guards. A visited set stops a container holding an ancestor from looping forever,
/// and a depth limit stops anything the visited set somehow misses. Neither is defensive padding:
/// without them a cycle hangs the request rather than answering wrongly.
/// </summary>
public sealed class CharacterInventoryReader
{
    /// <summary>Deep enough that no real bag tree reaches it, shallow enough that a cycle costs nothing.</summary>
    public const int MaxDepth = 10;

    private readonly IItemService _items;

    public CharacterInventoryReader(IItemService items)
    {
        _items = items;
    }

    /// <summary>The backpack's contents as a tree. Empty when the character has no backpack.</summary>
    public IReadOnlyList<CharacterItemResponse> ReadBackpack(MobileEntity mobile)
    {
        if (!mobile.BackpackId.IsValid || _items.GetById(mobile.BackpackId) is null)
        {
            return [];
        }

        // The backpack counts as visited before the walk starts, so an item inside it that points back
        // at it stops there instead of listing the whole bag again one level down.
        var visited = new HashSet<Serial> { mobile.BackpackId };

        return [.. _items.GetContents(mobile.BackpackId).Select(item => Read(item, visited, 1))];
    }

    /// <summary>The items worn on each layer, the backpack and the bank box among them.</summary>
    public IReadOnlyList<CharacterItemResponse> ReadEquipment(MobileEntity mobile)
    {
        var visited = new HashSet<Serial>();

        return [.. _items.GetEquipped(mobile).Select(item => Read(item, visited, 1))];
    }

    private CharacterItemResponse Read(ItemEntity item, HashSet<Serial> visited, int depth)
    {
        IReadOnlyList<CharacterItemResponse> contents = [];

        // Add returns false when this item has already been reported on this walk, which is the cycle
        // case: descending again would repeat everything under it, forever.
        if (depth < MaxDepth && visited.Add(item.Id))
        {
            contents = [.. _items.GetContents(item.Id).Select(child => Read(child, visited, depth + 1))];
        }

        return new(
            item.Id.ToString(),
            item.Name,
            item.TemplateId,
            item.ItemId,
            item.Hue.Value,
            item.Amount,
            item.EquippedLayer?.ToString(),
            contents
        );
    }
}
