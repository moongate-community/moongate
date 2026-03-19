using System.Globalization;
using Moongate.Server.Data.Internal.Scripting;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Interfaces.Services.Items;
using Moongate.UO.Data.Containers;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Interfaces.Templates;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Tiles;
using Moongate.UO.Data.Templates.Items;
using Moongate.UO.Data.Templates.Loot;
using Moongate.UO.Data.Types;
using Serilog;

namespace Moongate.Server.Services.Items;

/// <summary>
/// Generates weighted loot into container items the first time they are opened.
/// </summary>
public sealed class LootGenerationService : ILootGenerationService
{
    private readonly ILogger _logger = Log.ForContext<LootGenerationService>();
    private readonly IItemFactoryService _itemFactoryService;
    private readonly IItemService _itemService;
    private readonly ILootTemplateService _lootTemplateService;

    public LootGenerationService(
        IItemFactoryService itemFactoryService,
        IItemService itemService,
        ILootTemplateService lootTemplateService
    )
    {
        _itemFactoryService = itemFactoryService;
        _itemService = itemService;
        _lootTemplateService = lootTemplateService;
    }

    public async Task<UOItemEntity> EnsureLootGeneratedAsync(
        UOItemEntity container,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(container);
        _ = cancellationToken;

        if (!container.IsContainer)
        {
            return container;
        }

        if (container.TryGetCustomBoolean(ItemCustomParamKeys.Loot.Generated, out var alreadyGenerated) &&
            alreadyGenerated)
        {
            return container;
        }

        if (!TryResolveContainerTemplate(container, out var containerTemplate) ||
            containerTemplate is null ||
            containerTemplate.LootTables.Count == 0)
        {
            return container;
        }

        var layout = CreateLayout(container);
        var createdAnyLoot = false;

        foreach (var lootTableId in containerTemplate.LootTables)
        {
            if (string.IsNullOrWhiteSpace(lootTableId) ||
                !_lootTemplateService.TryGet(lootTableId.Trim(), out var lootTable) ||
                lootTable is null)
            {
                continue;
            }

            var rolledEntry = RollEntry(lootTable);

            if (rolledEntry is null)
            {
                continue;
            }

            var generatedItems = CreateItemsFromLootEntry(rolledEntry);

            foreach (var generatedItem in generatedItems)
            {
                var position = layout.FindNextAvailablePosition(generatedItem) ?? Point2D.Zero;
                layout.MarkSpaceOccupied(position, ContainerLayoutSystem.GetItemSize(generatedItem));
                await _itemService.CreateItemAsync(generatedItem);
                await _itemService.MoveItemToContainerAsync(generatedItem.Id, container.Id, position);
                createdAnyLoot = true;
            }
        }

        var refreshedContainer = await _itemService.GetItemAsync(container.Id) ?? container;
        refreshedContainer.SetCustomBoolean(ItemCustomParamKeys.Loot.Generated, true);
        await _itemService.UpsertItemAsync(refreshedContainer);

        if (createdAnyLoot)
        {
            _logger.Debug(
                "Generated first-open loot for container {ContainerId} using template {TemplateId}",
                container.Id,
                containerTemplate.Id
            );
        }

        return refreshedContainer;
    }

    private static ContainerLayout CreateLayout(UOItemEntity container)
    {
        var layout = new ContainerLayout(ContainerLayoutSystem.GetContainerSize(container));

        foreach (var containedItem in container.Items)
        {
            layout.MarkSpaceOccupied(containedItem.ContainerPosition, ContainerLayoutSystem.GetItemSize(containedItem));
        }

        return layout;
    }

    private List<UOItemEntity> CreateItemsFromLootEntry(LootTemplateEntry entry)
    {
        if (entry.Amount <= 0)
        {
            return [];
        }

        var item = CreateLootItem(entry);

        if (item is null)
        {
            return [];
        }

        if (entry.Amount == 1)
        {
            return [item];
        }

        if (item.IsStackable)
        {
            item.Amount = entry.Amount;

            return [item];
        }

        var items = new List<UOItemEntity>(entry.Amount)
        {
            item
        };

        for (var i = 1; i < entry.Amount; i++)
        {
            var duplicate = CreateLootItem(entry);

            if (duplicate is not null)
            {
                items.Add(duplicate);
            }
        }

        return items;
    }

    private UOItemEntity? CreateLootItem(LootTemplateEntry entry)
    {
        if (!string.IsNullOrWhiteSpace(entry.ItemTemplateId))
        {
            return _itemFactoryService.CreateItemFromTemplate(entry.ItemTemplateId.Trim());
        }

        if (string.IsNullOrWhiteSpace(entry.ItemId))
        {
            return null;
        }

        var rawItem = _itemFactoryService.CreateItemFromTemplate("static");
        rawItem.ItemId = ParseItemId(entry.ItemId);
        rawItem.GumpId = null;

        var tile = TileData.ItemTable[rawItem.ItemId];
        rawItem.Name = tile.Name;
        rawItem.Weight = tile.Weight;
        rawItem.IsStackable = tile[UOTileFlag.Generic];

        return rawItem;
    }

    private static int ParseItemId(string itemId)
    {
        var trimmed = itemId.Trim();

        if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return int.Parse(trimmed.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }

        return int.Parse(trimmed, CultureInfo.InvariantCulture);
    }

    private static LootTemplateEntry? RollEntry(LootTemplateDefinition definition)
    {
        var eligibleEntries = definition.Entries
                                        .Where(static entry =>
                                            entry.Weight > 0 &&
                                            (!string.IsNullOrWhiteSpace(entry.ItemTemplateId) ||
                                             !string.IsNullOrWhiteSpace(entry.ItemId)))
                                        .ToList();
        var totalWeight = definition.NoDropWeight + eligibleEntries.Sum(static entry => entry.Weight);

        if (totalWeight <= 0)
        {
            return null;
        }

        var roll = Random.Shared.Next(totalWeight);

        if (roll < definition.NoDropWeight)
        {
            return null;
        }

        roll -= definition.NoDropWeight;

        foreach (var entry in eligibleEntries)
        {
            if (roll < entry.Weight)
            {
                return entry;
            }

            roll -= entry.Weight;
        }

        return null;
    }

    private bool TryResolveContainerTemplate(UOItemEntity container, out ItemTemplateDefinition? template)
    {
        template = null;

        if (!container.TryGetCustomString(ItemCustomParamKeys.Item.TemplateId, out var templateId) ||
            string.IsNullOrWhiteSpace(templateId))
        {
            return false;
        }

        return _itemFactoryService.TryGetItemTemplate(templateId.Trim(), out template);
    }
}
