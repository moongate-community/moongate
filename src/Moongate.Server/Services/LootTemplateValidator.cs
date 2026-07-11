using Moongate.Server.Data.Internal;
using Moongate.UO.Data.Items;
using Moongate.UO.Data.Loot;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Services;

internal static class LootTemplateValidator
{
    public static void Validate(
        IReadOnlyList<LootTemplateSource> sources,
        IReadOnlyList<ItemTemplate> items
    )
    {
        var itemIds = new HashSet<string>(items.Select(item => item.Id), StringComparer.OrdinalIgnoreCase);
        var itemTags = new HashSet<string>(items.SelectMany(item => item.Tags), StringComparer.OrdinalIgnoreCase);
        var observedLootIds = new Dictionary<string, LootTemplateSource>(StringComparer.OrdinalIgnoreCase);

        foreach (var source in sources)
        {
            var template = source.Template;

            if (string.IsNullOrWhiteSpace(template.Id))
            {
                throw Error(source, "Id is required.");
            }

            if (string.IsNullOrWhiteSpace(template.Name))
            {
                throw Error(source, "Name is required.");
            }

            if (string.IsNullOrWhiteSpace(template.Category))
            {
                throw Error(source, "Category is required.");
            }

            if (!observedLootIds.TryAdd(template.Id, source))
            {
                throw Error(source, $"Duplicate loot template ID '{template.Id}'.");
            }

            if (template.Entries.Count == 0)
            {
                throw Error(source, "Loot template must contain at least one entry.");
            }

            if (template.Rolls <= 0)
            {
                throw Error(source, "Rolls must be greater than zero.");
            }

            if (template.NoDropWeight < 0)
            {
                throw Error(source, "NoDropWeight cannot be negative.");
            }

            for (var entryIndex = 0; entryIndex < template.Entries.Count; entryIndex++)
            {
                ValidateEntry(source, template.Entries[entryIndex], entryIndex, itemIds, itemTags);
            }
        }
    }

    private static void ValidateEntry(
        LootTemplateSource source,
        LootTemplateEntry entry,
        int entryIndex,
        HashSet<string> itemIds,
        HashSet<string> itemTags
    )
    {
        var hasItemTemplateId = !string.IsNullOrWhiteSpace(entry.ItemTemplateId);
        var hasItemTag = !string.IsNullOrWhiteSpace(entry.ItemTag);

        if (hasItemTemplateId == hasItemTag)
        {
            throw Error(source, "Entry must define exactly one item reference.", entryIndex);
        }

        if (hasItemTemplateId && !itemIds.Contains(entry.ItemTemplateId!))
        {
            throw Error(source, $"Unknown ItemTemplateId '{entry.ItemTemplateId}'.", entryIndex);
        }

        if (hasItemTag && !itemTags.Contains(entry.ItemTag!))
        {
            throw Error(source, $"Unknown ItemTag '{entry.ItemTag}'.", entryIndex);
        }

        var hasAmount = entry.Amount.HasValue;
        var hasMin = entry.AmountMin.HasValue;
        var hasMax = entry.AmountMax.HasValue;

        if (hasMin != hasMax)
        {
            throw Error(source, "Amount range requires both AmountMin and AmountMax.", entryIndex);
        }

        if (hasAmount && hasMin)
        {
            throw Error(source, "Entry cannot combine Amount with a range.", entryIndex);
        }

        if (!hasAmount && !hasMin)
        {
            throw Error(source, "Entry requires a fixed Amount or an amount range.", entryIndex);
        }

        if (hasAmount && entry.Amount <= 0)
        {
            throw Error(source, "Amount must be greater than zero.", entryIndex);
        }

        if (hasMin && (entry.AmountMin <= 0 || entry.AmountMax <= 0))
        {
            throw Error(source, "Amount range bounds must be greater than zero.", entryIndex);
        }

        if (hasMin && entry.AmountMin > entry.AmountMax)
        {
            throw Error(source, "AmountMin cannot exceed AmountMax.", entryIndex);
        }

        if (source.Template.Mode == LootTemplateModeType.Weighted)
        {
            if (!entry.Weight.HasValue || entry.Weight <= 0)
            {
                throw Error(source, "Weight must be greater than zero.", entryIndex);
            }

            if (entry.Chance.HasValue)
            {
                throw Error(source, "Weighted entry cannot define Chance.", entryIndex);
            }

            return;
        }

        if (!entry.Chance.HasValue)
        {
            throw Error(source, "Additive entry requires Chance.", entryIndex);
        }

        if (!double.IsFinite(entry.Chance.Value))
        {
            throw Error(source, "Chance must be finite.", entryIndex);
        }

        if (entry.Chance < 0 || entry.Chance > 1)
        {
            throw Error(source, "Chance must be between 0 and 1.", entryIndex);
        }

        if (entry.Weight.HasValue)
        {
            throw Error(source, "Additive entry cannot define Weight.", entryIndex);
        }
    }

    private static InvalidDataException Error(
        LootTemplateSource source,
        string message,
        int? entryIndex = null
    )
    {
        var templateId = string.IsNullOrWhiteSpace(source.Template.Id) ? "<unknown>" : source.Template.Id;
        var entry = entryIndex.HasValue ? $", entry {entryIndex.Value}" : "";
        return new InvalidDataException($"{source.RelativePath}: loot '{templateId}'{entry}: {message}");
    }
}
