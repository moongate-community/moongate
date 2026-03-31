using Moongate.Server.Attributes;
using Moongate.Server.Interfaces.Services.Files;
using Moongate.Server.Interfaces.Services.Scripting;
using Moongate.UO.Data.Containers;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Interfaces.Templates;
using Moongate.UO.Data.Services.Templates;
using Moongate.UO.Data.Templates.Items;
using Moongate.UO.Data.Templates.Loot;
using Moongate.UO.Data.Templates.Mobiles;
using Moongate.UO.Data.Templates.Quests;
using Moongate.UO.Data.Templates.SellProfiles;
using Moongate.UO.Data.Types;
using Serilog;

namespace Moongate.Server.FileLoaders;

/// <summary>
/// Validates loaded item and mobile templates and fails startup on invalid references or malformed entries.
/// </summary>
[RegisterFileLoader(16)]
public sealed class TemplateValidationLoader : IFileLoader
{
    private static readonly HashSet<string> AllowedFightModes = new(StringComparer.OrdinalIgnoreCase)
    {
        "none",
        "aggressor",
        "strongest",
        "weakest",
        "closest",
        "evil"
    };

    private readonly ILogger _logger = Log.ForContext<TemplateValidationLoader>();
    private readonly IItemTemplateService _itemTemplateService;
    private readonly IMobileTemplateService _mobileTemplateService;
    private readonly IFactionTemplateService _factionTemplateService;
    private readonly ISellProfileTemplateService _sellProfileTemplateService;
    private readonly IBookTemplateService _bookTemplateService;
    private readonly ILootTemplateService _lootTemplateService;
    private readonly IQuestTemplateService _questTemplateService;

    public TemplateValidationLoader(
        IItemTemplateService itemTemplateService,
        IMobileTemplateService mobileTemplateService,
        IFactionTemplateService factionTemplateService,
        ISellProfileTemplateService sellProfileTemplateService,
        IBookTemplateService bookTemplateService,
        ILootTemplateService lootTemplateService,
        IQuestTemplateService? questTemplateService = null
    )
    {
        _itemTemplateService = itemTemplateService;
        _mobileTemplateService = mobileTemplateService;
        _factionTemplateService = factionTemplateService;
        _sellProfileTemplateService = sellProfileTemplateService;
        _bookTemplateService = bookTemplateService;
        _lootTemplateService = lootTemplateService;
        _questTemplateService = questTemplateService ?? new QuestTemplateService();
    }

    public Task LoadAsync()
    {
        var errors = new List<string>();

        ValidateLootTemplates(errors);
        ValidateItems(errors);
        ValidateFactions(errors);
        ValidateSellProfiles(errors);
        ValidateMobiles(errors);
        ValidateQuests(errors);

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

    private void ValidateLootEntries(LootTemplateDefinition lootTemplate, List<string> errors)
    {
        for (var index = 0; index < lootTemplate.Entries.Count; index++)
        {
            var entry = lootTemplate.Entries[index];
            var entryLabel = $"Loot template '{lootTemplate.Id}' entry {index}";
            var referenceCount = 0;

            if (!string.IsNullOrWhiteSpace(entry.ItemTemplateId))
            {
                referenceCount++;

                if (!_itemTemplateService.TryGet(entry.ItemTemplateId.Trim(), out _))
                {
                    errors.Add($"{entryLabel} references missing item template '{entry.ItemTemplateId}'.");
                }
            }

            if (!string.IsNullOrWhiteSpace(entry.ItemId))
            {
                referenceCount++;
            }

            if (!string.IsNullOrWhiteSpace(entry.ItemTag))
            {
                referenceCount++;

                if (!_itemTemplateService.GetAll()
                                         .Any(
                                             item => item.Tags.Any(
                                                 tag => string.Equals(
                                                     tag,
                                                     entry.ItemTag.Trim(),
                                                     StringComparison.OrdinalIgnoreCase
                                                 )
                                             )
                                         )
                   )
                {
                    errors.Add($"{entryLabel} references unknown item tag '{entry.ItemTag}'.");
                }
            }

            if (referenceCount != 1)
            {
                errors.Add($"{entryLabel} must define exactly one of itemTemplateId, itemId, or itemTag.");
            }

            if (entry.Amount <= 0)
            {
                errors.Add($"{entryLabel} has non-positive amount {entry.Amount}.");
            }

            if (lootTemplate.Mode == LootTemplateMode.Weighted)
            {
                if (entry.Weight <= 0)
                {
                    errors.Add($"{entryLabel} has non-positive weight {entry.Weight}.");
                }

                if (entry.AmountMin.HasValue || entry.AmountMax.HasValue)
                {
                    errors.Add($"{entryLabel} cannot use amountMin/amountMax in weighted mode.");
                }
            }

            if (lootTemplate.Mode == LootTemplateMode.Additive)
            {
                if (entry.Weight != 1)
                {
                    errors.Add($"{entryLabel} must keep default weight 1 in additive mode.");
                }

                if (entry.Chance is < 0d or > 1d)
                {
                    errors.Add($"{entryLabel} has invalid chance {entry.Chance}.");
                }

                var hasAnyRange = entry.AmountMin.HasValue || entry.AmountMax.HasValue;

                if (entry.AmountMin.HasValue != entry.AmountMax.HasValue)
                {
                    errors.Add($"{entryLabel} must define both amountMin and amountMax together.");
                }

                if (hasAnyRange && entry.Amount != 1)
                {
                    errors.Add($"{entryLabel} cannot combine amount with amountMin/amountMax.");
                }

                if (entry.AmountMin.HasValue && entry.AmountMax.HasValue && entry.AmountMin.Value > entry.AmountMax.Value)
                {
                    errors.Add($"{entryLabel} has amountMin greater than amountMax.");
                }
            }
        }
    }

    private void ValidateLootTemplates(List<string> errors)
    {
        foreach (var lootTemplate in _lootTemplateService.GetAll())
        {
            if (string.IsNullOrWhiteSpace(lootTemplate.Id))
            {
                errors.Add("Loot template has empty id.");
            }

            if (string.IsNullOrWhiteSpace(lootTemplate.Name))
            {
                errors.Add($"Loot template '{lootTemplate.Id}' has empty name.");
            }

            if (lootTemplate.Entries.Count == 0)
            {
                errors.Add($"Loot template '{lootTemplate.Id}' has no entries.");

                continue;
            }

            if (lootTemplate.Mode == LootTemplateMode.Weighted)
            {
                if (lootTemplate.Rolls <= 0)
                {
                    errors.Add($"Loot template '{lootTemplate.Id}' has non-positive rolls {lootTemplate.Rolls}.");
                }

                if (lootTemplate.NoDropWeight < 0)
                {
                    errors.Add($"Loot template '{lootTemplate.Id}' has negative noDropWeight {lootTemplate.NoDropWeight}.");
                }
            }

            if (lootTemplate.Mode == LootTemplateMode.Additive)
            {
                if (lootTemplate.NoDropWeight != 0)
                {
                    errors.Add($"Loot template '{lootTemplate.Id}' cannot use noDropWeight in additive mode.");
                }
            }

            ValidateLootEntries(lootTemplate, errors);
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

            if (!string.IsNullOrWhiteSpace(mobile.SellProfileId) &&
                !_sellProfileTemplateService.TryGet(mobile.SellProfileId, out _))
            {
                errors.Add($"Mobile template '{mobile.Id}' references missing sell profile '{mobile.SellProfileId}'.");
            }

            ValidateMobileLootTables(mobile, errors);

            if (!string.IsNullOrWhiteSpace(mobile.DefaultFactionId) &&
                !_factionTemplateService.TryGet(mobile.DefaultFactionId, out _))
            {
                errors.Add($"Mobile template '{mobile.Id}' references missing default faction '{mobile.DefaultFactionId}'.");
            }

            ValidateMobileAi(mobile, errors);
            ValidateTemplateParams($"Mobile template '{mobile.Id}'", mobile.Params, errors);
            ValidateVariants(mobile, errors);
        }
    }

    private void ValidateQuestObjective(QuestTemplateDefinition quest, QuestObjectiveDefinition objective, int index, List<string> errors)
    {
        var objectiveLabel = $"Quest template '{quest.Id}' objective {index}";

        if (objective.Amount <= 0)
        {
            errors.Add($"{objectiveLabel} has non-positive amount {objective.Amount}.");
        }

        switch (objective.Type)
        {
            case QuestObjectiveType.Kill:
                if (objective.MobileTemplateIds.Count == 0)
                {
                    errors.Add($"{objectiveLabel} requires at least one mobile template id.");

                    return;
                }

                foreach (var mobileTemplateId in objective.MobileTemplateIds)
                {
                    if (string.IsNullOrWhiteSpace(mobileTemplateId))
                    {
                        errors.Add($"{objectiveLabel} has an empty mobile template id.");

                        continue;
                    }

                    if (!_mobileTemplateService.TryGet(mobileTemplateId.Trim(), out _))
                    {
                        errors.Add($"{objectiveLabel} references missing mobile template '{mobileTemplateId}'.");
                    }
                }

                break;
            case QuestObjectiveType.Collect:
            case QuestObjectiveType.Deliver:
                if (string.IsNullOrWhiteSpace(objective.ItemTemplateId))
                {
                    errors.Add($"{objectiveLabel} requires itemTemplateId.");

                    return;
                }

                if (!_itemTemplateService.TryGet(objective.ItemTemplateId.Trim(), out _))
                {
                    errors.Add($"{objectiveLabel} references missing item template '{objective.ItemTemplateId}'.");
                }

                break;
        }
    }

    private void ValidateQuestReward(QuestTemplateDefinition quest, QuestRewardDefinition reward, int index, List<string> errors)
    {
        var rewardLabel = $"Quest template '{quest.Id}' reward {index}";

        if (reward.Gold < 0)
        {
            errors.Add($"{rewardLabel} has negative gold {reward.Gold}.");
        }

        for (var itemIndex = 0; itemIndex < reward.Items.Count; itemIndex++)
        {
            var rewardItem = reward.Items[itemIndex];
            var rewardItemLabel = $"{rewardLabel} item {itemIndex}";

            if (string.IsNullOrWhiteSpace(rewardItem.ItemTemplateId))
            {
                errors.Add($"{rewardItemLabel} has empty itemTemplateId.");

                continue;
            }

            if (rewardItem.Amount <= 0)
            {
                errors.Add($"{rewardItemLabel} has non-positive amount {rewardItem.Amount}.");
            }

            if (!_itemTemplateService.TryGet(rewardItem.ItemTemplateId.Trim(), out _))
            {
                errors.Add($"{rewardItemLabel} references missing item template '{rewardItem.ItemTemplateId}'.");
            }
        }
    }

    private void ValidateQuests(List<string> errors)
    {
        foreach (var quest in _questTemplateService.GetAll())
        {
            if (string.IsNullOrWhiteSpace(quest.Id))
            {
                errors.Add("Quest template has empty id.");
            }

            if (string.IsNullOrWhiteSpace(quest.Name))
            {
                errors.Add($"Quest template '{quest.Id}' has empty name.");
            }

            if (quest.MaxActivePerCharacter != 1)
            {
                errors.Add($"Quest template '{quest.Id}' supports only one active instance per character.");
            }

            if (quest.QuestGiverTemplateIds.Count == 0)
            {
                errors.Add($"Quest template '{quest.Id}' has no quest giver template ids.");
            }

            foreach (var questGiverTemplateId in quest.QuestGiverTemplateIds)
            {
                if (string.IsNullOrWhiteSpace(questGiverTemplateId))
                {
                    errors.Add($"Quest template '{quest.Id}' has an empty quest giver template id.");

                    continue;
                }

                if (!_mobileTemplateService.TryGet(questGiverTemplateId.Trim(), out _))
                {
                    errors.Add($"Quest template '{quest.Id}' references missing quest giver template '{questGiverTemplateId}'.");
                }
            }

            if (quest.CompletionNpcTemplateIds.Count == 0)
            {
                errors.Add($"Quest template '{quest.Id}' has no completion npc template ids.");
            }

            foreach (var completionNpcTemplateId in quest.CompletionNpcTemplateIds)
            {
                if (string.IsNullOrWhiteSpace(completionNpcTemplateId))
                {
                    errors.Add($"Quest template '{quest.Id}' has an empty completion npc template id.");

                    continue;
                }

                if (!_mobileTemplateService.TryGet(completionNpcTemplateId.Trim(), out _))
                {
                    errors.Add($"Quest template '{quest.Id}' references missing completion npc template '{completionNpcTemplateId}'.");
                }
            }

            if (quest.Objectives.Count == 0)
            {
                errors.Add($"Quest template '{quest.Id}' has no objectives.");
            }

            for (var index = 0; index < quest.Objectives.Count; index++)
            {
                ValidateQuestObjective(quest, quest.Objectives[index], index, errors);
            }

            for (var index = 0; index < quest.Rewards.Count; index++)
            {
                ValidateQuestReward(quest, quest.Rewards[index], index, errors);
            }
        }
    }

    private static void ValidateMobileAi(MobileTemplateDefinition mobile, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(mobile.Ai.Brain))
        {
            errors.Add($"Mobile template '{mobile.Id}' has blank ai.brain.");
        }

        var fightMode = mobile.Ai.FightMode?.Trim();

        if (string.IsNullOrWhiteSpace(fightMode) || !AllowedFightModes.Contains(fightMode))
        {
            errors.Add($"Mobile template '{mobile.Id}' has invalid ai.fightMode '{mobile.Ai.FightMode}'.");
        }

        if (mobile.Ai.RangePerception is null || mobile.Ai.RangePerception <= 0)
        {
            errors.Add($"Mobile template '{mobile.Id}' has non-positive ai.rangePerception {mobile.Ai.RangePerception}.");
        }

        if (mobile.Ai.RangeFight is null || mobile.Ai.RangeFight < 0)
        {
            errors.Add($"Mobile template '{mobile.Id}' has negative ai.rangeFight {mobile.Ai.RangeFight}.");
        }
    }

    private void ValidateMobileLootTables(MobileTemplateDefinition mobile, List<string> errors)
    {
        foreach (var lootTableId in mobile.LootTables)
        {
            if (string.IsNullOrWhiteSpace(lootTableId))
            {
                errors.Add($"Mobile template '{mobile.Id}' has an empty loot table id.");

                continue;
            }

            if (!_lootTemplateService.TryGet(lootTableId.Trim(), out _))
            {
                errors.Add($"Mobile template '{mobile.Id}' references missing loot table '{lootTableId}'.");
            }
        }
    }

    private void ValidateEquipmentEntry(
        MobileTemplateDefinition mobile,
        MobileVariantTemplate variant,
        MobileEquipmentEntryTemplate equipment,
        int equipmentIndex,
        List<string> errors
    )
    {
        var hasItemTemplateId = !string.IsNullOrWhiteSpace(equipment.ItemTemplateId);
        var hasWeightedItems = equipment.Items.Count > 0;
        var equipmentLabel = $"Mobile template '{mobile.Id}' variant '{variant.Name}' equipment {equipmentIndex}";

        if (equipment.Layer == ItemLayerType.Invalid)
        {
            errors.Add($"{equipmentLabel} has invalid layer.");
        }

        if (equipment.Chance is < 0d or > 1d)
        {
            errors.Add($"{equipmentLabel} has invalid chance {equipment.Chance}.");
        }

        if (hasItemTemplateId == hasWeightedItems)
        {
            errors.Add($"{equipmentLabel} must define exactly one of itemTemplateId or items.");
        }

        if (hasItemTemplateId)
        {
            var itemTemplateId = equipment.ItemTemplateId!.Trim();

            if (!_itemTemplateService.TryGet(itemTemplateId, out _))
            {
                errors.Add(
                    $"Mobile template '{mobile.Id}' variant '{variant.Name}' equipment {equipmentIndex} references missing item template '{equipment.ItemTemplateId}'."
                );
            }
        }

        ValidateTemplateParams(equipmentLabel, equipment.Params, errors);

        if (!hasWeightedItems)
        {
            return;
        }

        for (var index = 0; index < equipment.Items.Count; index++)
        {
            var weightedItem = equipment.Items[index];
            var weightedItemLabel = $"{equipmentLabel} item {index}";

            if (string.IsNullOrWhiteSpace(weightedItem.ItemTemplateId))
            {
                errors.Add($"{weightedItemLabel} has empty itemTemplateId.");

                continue;
            }

            if (weightedItem.Weight <= 0)
            {
                errors.Add($"{weightedItemLabel} has non-positive weight {weightedItem.Weight}.");
            }

            if (!_itemTemplateService.TryGet(weightedItem.ItemTemplateId.Trim(), out _))
            {
                errors.Add(
                    $"Mobile template '{mobile.Id}' variant '{variant.Name}' equipment {equipmentIndex} references missing item template '{weightedItem.ItemTemplateId}'."
                );
            }

            ValidateTemplateParams(weightedItemLabel, weightedItem.Params, errors);
        }
    }

    private void ValidateVariants(MobileTemplateDefinition mobile, List<string> errors)
    {
        if (mobile.Variants.Count == 0)
        {
            errors.Add($"Mobile template '{mobile.Id}' has no variants.");

            return;
        }

        for (var index = 0; index < mobile.Variants.Count; index++)
        {
            var variant = mobile.Variants[index];
            var variantLabel = $"Mobile template '{mobile.Id}' variant {index}";

            if (variant.Weight <= 0)
            {
                errors.Add($"{variantLabel} has non-positive weight {variant.Weight}.");
            }

            if (variant.Appearance.Body <= 0)
            {
                errors.Add($"{variantLabel} has invalid or missing appearance.body.");
            }

            for (var equipmentIndex = 0; equipmentIndex < variant.Equipment.Count; equipmentIndex++)
            {
                ValidateEquipmentEntry(mobile, variant, variant.Equipment[equipmentIndex], equipmentIndex, errors);
            }
        }
    }

    private static void ValidateTemplateParams(
        string ownerLabel,
        IReadOnlyDictionary<string, ItemTemplateParamDefinition> parameters,
        List<string> errors
    )
    {
        foreach (var (key, param) in parameters)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                errors.Add($"{ownerLabel} has an invalid params entry with an empty key.");

                continue;
            }

            var normalizedKey = key.Trim();

            switch (param.Type)
            {
                case ItemTemplateParamType.String:
                    break;
                case ItemTemplateParamType.Serial:
                    if (!Serial.TryParse(param.Value, null, out _))
                    {
                        errors.Add($"{ownerLabel} has invalid serial param '{normalizedKey}' = '{param.Value}'.");
                    }

                    break;
                case ItemTemplateParamType.Hue:
                    try
                    {
                        HueSpec.ParseFromString(param.Value).Resolve();
                    }
                    catch (FormatException)
                    {
                        errors.Add($"{ownerLabel} has invalid hue param '{normalizedKey}' = '{param.Value}'.");
                    }

                    break;
                default:
                    errors.Add($"{ownerLabel} has unsupported param type '{param.Type}' for key '{normalizedKey}'.");

                    break;
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
