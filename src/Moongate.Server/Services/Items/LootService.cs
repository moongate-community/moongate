using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Interfaces.Items;
using Moongate.UO.Data.Loot;
using Moongate.UO.Data.Types;
using Serilog;

namespace Moongate.Server.Services.Items;

/// <summary>Rolls loot tables (Weighted/Additive) into items via the item factory. Pure: does not persist.</summary>
public sealed class LootService : ILootService
{
    private readonly ILogger _logger = Log.ForContext<LootService>();
    private readonly ILootTemplateService _templates;
    private readonly IItemFactoryService _itemFactory;
    private readonly Random _random;

    public LootService(ILootTemplateService templates, IItemFactoryService itemFactory, Random random)
    {
        _templates = templates;
        _itemFactory = itemFactory;
        _random = random;
    }

    public IReadOnlyList<ItemEntity> Roll(string lootTableId)
    {
        var template = _templates.GetById(lootTableId);

        if (template is null)
        {
            _logger.Warning("Unknown loot table '{LootTableId}'", lootTableId);

            return [];
        }

        var rolls = Math.Max(1, template.Rolls);
        var result = new List<ItemEntity>();

        for (var roll = 0; roll < rolls; roll++)
        {
            if (template.Mode == LootTemplateModeType.Additive)
            {
                foreach (var entry in template.Entries)
                {
                    if (_random.NextDouble() < (entry.Chance ?? 1.0))
                    {
                        result.AddRange(Materialize(entry));
                    }
                }

                continue;
            }

            var picked = PickWeighted(template);

            if (picked is not null)
            {
                result.AddRange(Materialize(picked));
            }
        }

        return result;
    }

    private IReadOnlyList<ItemEntity> Materialize(LootTemplateEntry entry)
    {
        var amount = entry is { AmountMin: { } low, AmountMax: { } high }
                         ? _random.Next(low, high + 1)
                         : entry.Amount ?? 1;

        var hasId = !string.IsNullOrWhiteSpace(entry.ItemTemplateId);
        var hasTag = !string.IsNullOrWhiteSpace(entry.ItemTag);

        if (hasId == hasTag)
        {
            _logger.Warning("Loot entry must set exactly one of ItemTemplateId/ItemTag; skipping");

            return [];
        }

        return hasId
                   ? _itemFactory.CreateFromTemplate(entry.ItemTemplateId!, amount: amount)
                   : _itemFactory.CreateByTag(entry.ItemTag!, amount: amount);
    }

    private LootTemplateEntry? PickWeighted(LootTemplate template)
    {
        var total = Math.Max(0, template.NoDropWeight);

        foreach (var entry in template.Entries)
        {
            total += Math.Max(1, entry.Weight ?? 1);
        }

        if (total == 0)
        {
            return null;
        }

        var roll = _random.Next(total);
        var cumulative = 0;

        foreach (var entry in template.Entries)
        {
            cumulative += Math.Max(1, entry.Weight ?? 1);

            if (roll < cumulative)
            {
                return entry;
            }
        }

        return null;
    }
}
