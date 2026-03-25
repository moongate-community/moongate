using Moongate.Server.Interfaces.Items;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Modules;

internal sealed class MobileInventoryModule
{
    private readonly IItemService? _itemService;

    public MobileInventoryModule(IItemService? itemService)
    {
        _itemService = itemService;
    }

    public UOItemEntity? AddItemToBackpack(UOMobileEntity mobile, string itemTemplateId, int amount = 1)
    {
        ArgumentNullException.ThrowIfNull(mobile);

        if (_itemService is null || string.IsNullOrWhiteSpace(itemTemplateId) || amount <= 0)
        {
            return null;
        }

        var backpackId = ResolveBackpackId(mobile);

        if (backpackId == Serial.Zero)
        {
            return null;
        }

        var item = _itemService.SpawnFromTemplateAsync(itemTemplateId.Trim()).GetAwaiter().GetResult();

        if (amount > 1)
        {
            if (!item.IsStackable)
            {
                return null;
            }

            item.Amount = amount;
            _itemService.UpsertItemAsync(item).GetAwaiter().GetResult();
        }

        var moved = _itemService.MoveItemToContainerAsync(item.Id, backpackId, new(1, 1)).GetAwaiter().GetResult();

        return moved ? item : null;
    }

    public bool ConsumeItem(UOMobileEntity mobile, int itemId, int amount = 1)
    {
        ArgumentNullException.ThrowIfNull(mobile);

        if (_itemService is null || itemId <= 0 || amount <= 0)
        {
            return false;
        }

        var sources = GetConsumableSources(mobile).ToList();

        if (CountMatchingItems(sources, itemId) < amount)
        {
            return false;
        }

        for (var i = 0; i < amount; i++)
        {
            if (!TryConsumeFromSources(sources, itemId))
            {
                return false;
            }
        }

        return true;
    }

    private static int CountMatchingItems(IEnumerable<UOItemEntity> containers, int itemId)
        => containers.Sum(container => CountMatchingItems(container, itemId));

    private static int CountMatchingItems(UOItemEntity container, int itemId)
    {
        var count = 0;

        foreach (var child in container.Items)
        {
            if (child.ItemId == itemId)
            {
                count += Math.Max(0, child.Amount);
            }

            if (child.Items.Count > 0)
            {
                count += CountMatchingItems(child, itemId);
            }
        }

        return count;
    }

    private static IEnumerable<UOItemEntity> GetConsumableSources(UOMobileEntity mobile)
    {
        var quiver = mobile.GetEquippedItemsRuntime().FirstOrDefault(static item => item.IsQuiver);

        if (quiver is not null)
        {
            yield return quiver;
        }

        var backpack = TryResolveBackpack(mobile);

        if (backpack is not null)
        {
            yield return backpack;
        }
    }

    private static Serial ResolveBackpackId(UOMobileEntity mobile)
    {
        if (mobile.BackpackId != Serial.Zero)
        {
            return mobile.BackpackId;
        }

        return mobile.EquippedItemIds.TryGetValue(ItemLayerType.Backpack, out var equippedBackpackId)
                   ? equippedBackpackId
                   : Serial.Zero;
    }

    private static UOItemEntity? TryResolveBackpack(UOMobileEntity mobile)
    {
        var backpackId = ResolveBackpackId(mobile);

        return mobile.GetEquippedItemsRuntime()
                     .FirstOrDefault(item => item.Id == backpackId || item.EquippedLayer == ItemLayerType.Backpack);
    }

    private bool TryConsumeFromSources(IEnumerable<UOItemEntity> sources, int itemId)
    {
        foreach (var source in sources)
        {
            if (!TryConsumeItemRecursive(source, itemId, out var changedStack, out var deletedStack))
            {
                continue;
            }

            if (changedStack is not null)
            {
                _itemService!.UpsertItemAsync(changedStack).GetAwaiter().GetResult();
            }

            if (deletedStack is not null)
            {
                _ = _itemService!.DeleteItemAsync(deletedStack.Id).GetAwaiter().GetResult();
            }

            _itemService!.UpsertItemAsync(source).GetAwaiter().GetResult();

            return true;
        }

        return false;
    }

    private static bool TryConsumeItemRecursive(
        UOItemEntity container,
        int itemId,
        out UOItemEntity? changedStack,
        out UOItemEntity? deletedStack
    )
    {
        changedStack = null;
        deletedStack = null;

        for (var index = container.Items.Count - 1; index >= 0; index--)
        {
            var child = container.Items[index];

            if (child.ItemId == itemId)
            {
                child.Amount--;

                if (child.Amount <= 0)
                {
                    container.RemoveItem(child.Id);
                    deletedStack = child;
                }
                else
                {
                    changedStack = child;
                }

                return true;
            }

            if (TryConsumeItemRecursive(child, itemId, out changedStack, out deletedStack))
            {
                return true;
            }
        }

        return false;
    }
}
