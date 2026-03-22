using Moongate.Server.Attributes;
using Moongate.Server.Interfaces.Services.Scripting;
using Moongate.UO.Data.Containers;
using Moongate.Server.Interfaces.Services.Files;
using Moongate.UO.Data.Interfaces.Templates;
using Moongate.UO.Data.Templates.Factions;
using Moongate.UO.Data.Templates.Items;
using Moongate.UO.Data.Templates.Mobiles;
using Moongate.UO.Data.Templates.SellProfiles;
using Serilog;

namespace Moongate.Server.FileLoaders;

/// <summary>
/// Validates loaded item and mobile templates and fails startup on invalid references or malformed entries.
/// </summary>
[RegisterFileLoader(15)]
public sealed class TemplateValidationLoader : IFileLoader
{
    private readonly ILogger _logger = Log.ForContext<TemplateValidationLoader>();
    private readonly IItemTemplateService _itemTemplateService;
    private readonly IMobileTemplateService _mobileTemplateService;
    private readonly IFactionTemplateService _factionTemplateService;
    private readonly ISellProfileTemplateService _sellProfileTemplateService;
    private readonly IBookTemplateService _bookTemplateService;
    private readonly ILootTemplateService _lootTemplateService;

    public TemplateValidationLoader(
        IItemTemplateService itemTemplateService,
        IMobileTemplateService mobileTemplateService,
        IFactionTemplateService factionTemplateService,
        ISellProfileTemplateService sellProfileTemplateService,
        IBookTemplateService bookTemplateService,
        ILootTemplateService lootTemplateService
    )
    {
        _itemTemplateService = itemTemplateService;
        _mobileTemplateService = mobileTemplateService;
        _factionTemplateService = factionTemplateService;
        _sellProfileTemplateService = sellProfileTemplateService;
        _bookTemplateService = bookTemplateService;
        _lootTemplateService = lootTemplateService;
    }

    public Task LoadAsync()
    {
        var errors = new List<string>();

        ValidateItems(errors);
        ValidateFactions(errors);
        ValidateSellProfiles(errors);
        ValidateMobiles(errors);

        if (errors.Count == 0)
        {
            _logger.Information(
                "Template validation completed successfully ({ItemCount} item templates, {MobileCount} mobile templates)",
                _itemTemplateService.Count,
                _mobileTemplateService.Count
            );

            return Task.CompletedTask;
        }

        foreach (var error in errors)
        {
            _logger.Error("Template validation error: {Error}", error);
        }

        throw new InvalidOperationException($"Template validation failed with {errors.Count} error(s).");
    }

    private void ValidateBookTemplate(ItemTemplateDefinition item, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(item.BookId))
        {
            return;
        }

        if (!_bookTemplateService.TryLoad(item.BookId, null, out _))
        {
            errors.Add($"Item template '{item.Id}' references missing or invalid book template '{item.BookId}'.");
        }
    }

    private void ValidateFixedEquipment(MobileTemplateDefinition mobile, List<string> errors)
    {
        foreach (var fixedEquipment in mobile.FixedEquipment)
        {
            if (string.IsNullOrWhiteSpace(fixedEquipment.ItemTemplateId))
            {
                errors.Add($"Mobile template '{mobile.Id}' has fixed equipment with empty itemTemplateId.");

                continue;
            }

            if (!_itemTemplateService.TryGet(fixedEquipment.ItemTemplateId, out _))
            {
                errors.Add(
                    $"Mobile template '{mobile.Id}' references missing fixed item template '{fixedEquipment.ItemTemplateId}'."
                );
            }
        }
    }

    private static void ValidateItemContainerLayout(
        ItemTemplateDefinition item,
        List<string> errors
    )
    {
        var isContainerTemplate = item.Tags.Any(
            static tag => string.Equals(tag, "container", StringComparison.OrdinalIgnoreCase)
        );

        if (!isContainerTemplate)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(item.ContainerLayoutId))
        {
            errors.Add($"Item template '{item.Id}' is a container but has no containerLayoutId.");

            return;
        }

        if (!ContainerLayoutSystem.ContainerSizesById.ContainsKey(item.ContainerLayoutId))
        {
            errors.Add($"Item template '{item.Id}' references unknown containerLayoutId '{item.ContainerLayoutId}'.");
        }
    }

    private void ValidateItems(List<string> errors)
    {
        foreach (var item in _itemTemplateService.GetAll())
        {
            if (string.IsNullOrWhiteSpace(item.Id))
            {
                errors.Add("Item template has empty id.");
            }

            if (string.IsNullOrWhiteSpace(item.Name))
            {
                errors.Add($"Item template '{item.Id}' has empty name.");
            }

            if (string.IsNullOrWhiteSpace(item.ItemId))
            {
                errors.Add($"Item template '{item.Id}' has empty itemId.");
            }

            if (item.Weight < 0)
            {
                errors.Add($"Item template '{item.Id}' has negative weight: {item.Weight}.");
            }

            ValidateBookTemplate(item, errors);
            ValidateItemContainerLayout(item, errors);
            ValidateItemLootTables(item, errors);
        }
    }

    private void ValidateItemLootTables(ItemTemplateDefinition item, List<string> errors)
    {
        foreach (var lootTableId in item.LootTables)
        {
            if (string.IsNullOrWhiteSpace(lootTableId))
            {
                errors.Add($"Item template '{item.Id}' has an empty loot table id.");

                continue;
            }

            if (!_lootTemplateService.TryGet(lootTableId.Trim(), out _))
            {
                errors.Add($"Item template '{item.Id}' references missing loot table '{lootTableId}'.");
            }
        }
    }

    private void ValidateMobiles(List<string> errors)
    {
        foreach (var mobile in _mobileTemplateService.GetAll())
        {
            if (string.IsNullOrWhiteSpace(mobile.Id))
            {
                errors.Add("Mobile template has empty id.");
            }

            if (mobile.Body < 0)
            {
                errors.Add($"Mobile template '{mobile.Id}' has invalid body: {mobile.Body}.");
            }

            if (!string.IsNullOrWhiteSpace(mobile.SellProfileId) &&
                !_sellProfileTemplateService.TryGet(mobile.SellProfileId, out _))
            {
                errors.Add($"Mobile template '{mobile.Id}' references missing sell profile '{mobile.SellProfileId}'.");
            }

            if (!string.IsNullOrWhiteSpace(mobile.DefaultFactionId) &&
                !_factionTemplateService.TryGet(mobile.DefaultFactionId, out _))
            {
                errors.Add($"Mobile template '{mobile.Id}' references missing default faction '{mobile.DefaultFactionId}'.");
            }

            ValidateFixedEquipment(mobile, errors);
            ValidateRandomEquipment(mobile, errors);
        }
    }

    private void ValidateFactions(List<string> errors)
    {
        foreach (var faction in _factionTemplateService.GetAll())
        {
            foreach (var enemyFactionId in faction.EnemyFactionIds)
            {
                if (string.IsNullOrWhiteSpace(enemyFactionId))
                {
                    errors.Add($"Faction template '{faction.Id}' has an empty enemy faction id.");

                    continue;
                }

                if (!_factionTemplateService.TryGet(enemyFactionId.Trim(), out _))
                {
                    errors.Add($"Faction template '{faction.Id}' references missing enemy faction '{enemyFactionId}'.");
                }
            }
        }
    }

    private void ValidateRandomEquipment(MobileTemplateDefinition mobile, List<string> errors)
    {
        foreach (var randomPool in mobile.RandomEquipment)
        {
            if (randomPool.SpawnChance is < 0f or > 1f)
            {
                errors.Add(
                    $"Mobile template '{mobile.Id}' random pool '{randomPool.Name}' has invalid spawnChance {randomPool.SpawnChance}."
                );
            }

            if (randomPool.Items.Count == 0)
            {
                errors.Add($"Mobile template '{mobile.Id}' random pool '{randomPool.Name}' has no items.");
            }

            foreach (var weightedItem in randomPool.Items)
            {
                if (string.IsNullOrWhiteSpace(weightedItem.ItemTemplateId))
                {
                    errors.Add($"Mobile template '{mobile.Id}' random pool '{randomPool.Name}' has empty itemTemplateId.");

                    continue;
                }

                if (weightedItem.Weight <= 0)
                {
                    errors.Add(
                        $"Mobile template '{mobile.Id}' random pool '{randomPool.Name}' has non-positive weight for item '{weightedItem.ItemTemplateId}': {weightedItem.Weight}."
                    );
                }

                if (!_itemTemplateService.TryGet(weightedItem.ItemTemplateId, out _))
                {
                    errors.Add(
                        $"Mobile template '{mobile.Id}' random pool '{randomPool.Name}' references missing item template '{weightedItem.ItemTemplateId}'."
                    );
                }
            }
        }
    }

    private void ValidateSellProfileAcceptedItems(SellProfileTemplateDefinition sellProfile, List<string> errors)
    {
        foreach (var acceptedItem in sellProfile.AcceptedItems)
        {
            if (acceptedItem.Price < 0)
            {
                errors.Add($"Sell profile '{sellProfile.Id}' has accepted item with negative price ({acceptedItem.Price}).");
            }

            if (!string.IsNullOrWhiteSpace(acceptedItem.ItemTemplateId) &&
                !_itemTemplateService.TryGet(acceptedItem.ItemTemplateId, out _))
            {
                errors.Add(
                    $"Sell profile '{sellProfile.Id}' references missing accepted item template '{acceptedItem.ItemTemplateId}'."
                );
            }
        }
    }

    private void ValidateSellProfiles(List<string> errors)
    {
        foreach (var sellProfile in _sellProfileTemplateService.GetAll())
        {
            ValidateSellProfileVendorItems(sellProfile, errors);
            ValidateSellProfileAcceptedItems(sellProfile, errors);
        }
    }

    private void ValidateSellProfileVendorItems(SellProfileTemplateDefinition sellProfile, List<string> errors)
    {
        foreach (var vendorItem in sellProfile.VendorItems)
        {
            if (string.IsNullOrWhiteSpace(vendorItem.ItemTemplateId))
            {
                errors.Add($"Sell profile '{sellProfile.Id}' has vendor item with empty itemTemplateId.");

                continue;
            }

            if (!_itemTemplateService.TryGet(vendorItem.ItemTemplateId, out _))
            {
                errors.Add(
                    $"Sell profile '{sellProfile.Id}' references missing vendor item template '{vendorItem.ItemTemplateId}'."
                );
            }

            if (vendorItem.Price < 0)
            {
                errors.Add($"Sell profile '{sellProfile.Id}' vendor item '{vendorItem.ItemTemplateId}' has negative price.");
            }

            if (vendorItem.MaxStock < 0)
            {
                errors.Add(
                    $"Sell profile '{sellProfile.Id}' vendor item '{vendorItem.ItemTemplateId}' has negative maxStock."
                );
            }
        }
    }
}
