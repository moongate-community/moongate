using System.Globalization;
using Moongate.Server.Data.Internal.Scripting;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Interfaces.Services.Items;
using Moongate.Server.Types.Items;
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
    private readonly IItemTemplateService _itemTemplateService;

    public LootGenerationService(
        IItemFactoryService itemFactoryService,
        IItemService itemService,
        ILootTemplateService lootTemplateService,
        IItemTemplateService itemTemplateService
    )
    {
        _itemFactoryService = itemFactoryService;
        _itemService = itemService;
        _lootTemplateService = lootTemplateService;
        _itemTemplateService = itemTemplateService;
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

        if (!TryResolveContainerTemplate(container, out var containerTemplate) ||
            containerTemplate is null ||
            containerTemplate.LootTables.Count == 0)
        {
            return container;
        }

        var refillDelay = LootContainerTemplateHelper.GetRefillDelay(containerTemplate);
        var alreadyGenerated = container.TryGetCustomBoolean(ItemCustomParamKeys.Loot.Generated, out var generated) &&
                               generated;
        var refillDue = alreadyGenerated && IsRefillDue(container, refillDelay);

        if (alreadyGenerated && !refillDue)
        {
            return container;
        }

        return await GenerateLootAsync(
            container,
            containerTemplate.LootTables,
            LootGenerationMode.FirstOpen,
            containerTemplate.Id,
            refillDelay,
            cancellationToken
        );
    }

    public async Task<UOItemEntity> GenerateForContainerAsync(
        UOItemEntity container,
        IReadOnlyList<string> lootTableIds,
        LootGenerationMode mode,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(container);
        ArgumentNullException.ThrowIfNull(lootTableIds);
        _ = cancellationToken;

        if (!container.IsContainer || lootTableIds.Count == 0)
        {
            return container;
        }

        return await GenerateLootAsync(
            container,
            lootTableIds,
            mode,
            null,
            null,
            cancellationToken
        );
    }

    private async Task<UOItemEntity> GenerateLootAsync(
        UOItemEntity container,
        IReadOnlyList<string> lootTableIds,
        LootGenerationMode mode,
        string? sourceTemplateId,
        TimeSpan? refillDelay,
        CancellationToken cancellationToken
    )
    {
        var layout = CreateLayout(container);
        var createdAnyLoot = false;

        foreach (var lootTableId in lootTableIds)
        {
            if (string.IsNullOrWhiteSpace(lootTableId) ||
                !_lootTemplateService.TryGet(lootTableId.Trim(), out var lootTable) ||
                lootTable is null)
            {
                continue;
            }

            var generatedItems = CreateItemsFromLootTable(lootTable);

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

        if (mode == LootGenerationMode.FirstOpen)
        {
            refreshedContainer.SetCustomBoolean(ItemCustomParamKeys.Loot.Generated, true);

            if (createdAnyLoot || refillDelay is null)
            {
                refreshedContainer.RemoveCustomProperty(ItemCustomParamKeys.Loot.RefillReadyAtUtc);
            }
            else if (refreshedContainer.Items.Count == 0)
            {
                var nextRefillReadyAtUtc = DateTime.UtcNow.Add(refillDelay.Value);
                refreshedContainer.SetCustomString(
                    ItemCustomParamKeys.Loot.RefillReadyAtUtc,
                    nextRefillReadyAtUtc.ToString("O", CultureInfo.InvariantCulture)
                );
            }
        }

        await _itemService.UpsertItemAsync(refreshedContainer);

        if (createdAnyLoot)
        {
            _logger.Debug(
                "Generated container loot for container {ContainerId} using template {TemplateId}",
                container.Id,
                sourceTemplateId ?? "explicit"
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

    private List<UOItemEntity> CreateItemsFromLootEntry(LootTemplateEntry entry, int amount)
    {
        if (amount <= 0)
        {
            return [];
        }

        var item = CreateLootItem(entry);

        if (item is null)
        {
            return [];
        }

        if (amount == 1)
        {
            return [item];
        }

        if (item.IsStackable)
        {
            item.Amount = amount;

            return [item];
        }

        var items = new List<UOItemEntity>(amount)
        {
            item
        };

        for (var i = 1; i < amount; i++)
        {
            var duplicate = CreateLootItem(entry);

            if (duplicate is not null)
            {
                items.Add(duplicate);
            }
        }

        return items;
    }

    private List<UOItemEntity> CreateItemsFromAdditiveLootTable(LootTemplateDefinition lootTable)
    {
        var items = new List<UOItemEntity>();

        foreach (var entry in lootTable.Entries)
        {
            if (!ShouldGenerateAdditiveEntry(entry))
            {
                continue;
            }

            var amount = ResolveEntryAmount(entry);

            if (amount <= 0)
            {
                continue;
            }

            items.AddRange(CreateItemsFromLootEntry(entry, amount));
        }

        return items;
    }

    private List<UOItemEntity> CreateItemsFromLootTable(LootTemplateDefinition lootTable)
    {
        return lootTable.Mode switch
        {
            LootTemplateMode.Additive => CreateItemsFromAdditiveLootTable(lootTable),
            _ => CreateItemsFromWeightedLootTable(lootTable)
        };
    }

    private List<UOItemEntity> CreateItemsFromWeightedLootTable(LootTemplateDefinition lootTable)
    {
        var items = new List<UOItemEntity>();
        var rolls = Math.Max(1, lootTable.Rolls);

        for (var rollIndex = 0; rollIndex < rolls; rollIndex++)
        {
            var rolledEntry = RollEntry(lootTable);

            if (rolledEntry is null)
            {
                continue;
            }

            items.AddRange(CreateItemsFromLootEntry(rolledEntry, rolledEntry.Amount));
        }

        return items;
    }

    private UOItemEntity? CreateLootItem(LootTemplateEntry entry)
    {
        if (!string.IsNullOrWhiteSpace(entry.ItemTemplateId))
        {
            return _itemFactoryService.CreateItemFromTemplate(entry.ItemTemplateId.Trim());
        }

        if (!string.IsNullOrWhiteSpace(entry.ItemTag))
        {
            var taggedTemplate = ResolveTemplateByTag(entry.ItemTag.Trim());

            if (taggedTemplate is not null)
            {
                return _itemFactoryService.CreateItemFromTemplate(taggedTemplate.Id);
            }
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

    private ItemTemplateDefinition? ResolveTemplateByTag(string itemTag)
    {
        var taggedTemplates = _itemTemplateService.GetAll()
            .Where(template => template.Tags.Any(tag => string.Equals(tag, itemTag, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(template => template.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (taggedTemplates.Count == 0)
        {
            return null;
        }

        return taggedTemplates[Random.Shared.Next(taggedTemplates.Count)];
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
                                             !string.IsNullOrWhiteSpace(entry.ItemTag) ||
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

    private static int ResolveEntryAmount(LootTemplateEntry entry)
    {
        if (entry.AmountMin.HasValue && entry.AmountMax.HasValue)
        {
            if (entry.AmountMin.Value == entry.AmountMax.Value)
            {
                return entry.AmountMin.Value;
            }

            return Random.Shared.Next(entry.AmountMin.Value, entry.AmountMax.Value + 1);
        }

        return entry.Amount;
    }

    private static bool ShouldGenerateAdditiveEntry(LootTemplateEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.ItemTemplateId) &&
            string.IsNullOrWhiteSpace(entry.ItemTag) &&
            string.IsNullOrWhiteSpace(entry.ItemId))
        {
            return false;
        }

        if (entry.Chance <= 0d)
        {
            return false;
        }

        if (entry.Chance >= 1d)
        {
            return true;
        }

        return Random.Shared.NextDouble() <= entry.Chance;
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

    private static bool IsRefillDue(UOItemEntity container, TimeSpan? refillDelay)
    {
        if (refillDelay is null || container.Items.Count > 0)
        {
            return false;
        }

        return container.TryGetCustomString(ItemCustomParamKeys.Loot.RefillReadyAtUtc, out var refillReadyAtRaw) &&
               DateTime.TryParse(
                   refillReadyAtRaw,
                   CultureInfo.InvariantCulture,
                   DateTimeStyles.RoundtripKind,
                   out var refillReadyAtUtc
               ) &&
               refillReadyAtUtc <= DateTime.UtcNow;
    }
}
