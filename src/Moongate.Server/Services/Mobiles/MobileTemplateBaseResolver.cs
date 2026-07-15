using Moongate.UO.Data.Mobiles.Templates;

namespace Moongate.Server.Services.Mobiles;

/// <summary>
/// Flattens <c>BaseMobile</c> inheritance at load time. Each template is merged onto its (recursively
/// resolved) base: strings and dictionaries/lists overlay, numeric scalars take the derived value when
/// it differs from the DTO default. Unknown bases and cycles are load errors.
/// </summary>
public sealed class MobileTemplateBaseResolver
{
    public IReadOnlyList<MobileTemplate> Resolve(IReadOnlyList<MobileTemplate> templates)
    {
        var byId = new Dictionary<string, MobileTemplate>(StringComparer.OrdinalIgnoreCase);

        foreach (var template in templates)
        {
            byId[template.Id] = template;
        }

        return templates.Select(template => ResolveOne(template, byId, new(StringComparer.OrdinalIgnoreCase))).ToList();
    }

    private MobileTemplate ResolveOne(MobileTemplate template, Dictionary<string, MobileTemplate> byId, HashSet<string> visiting)
    {
        if (string.IsNullOrEmpty(template.BaseMobile))
        {
            return template;
        }

        if (!visiting.Add(template.Id))
        {
            throw new InvalidDataException($"Mobile template inheritance cycle detected at '{template.Id}'.");
        }

        if (!byId.TryGetValue(template.BaseMobile, out var baseTemplate))
        {
            throw new InvalidDataException(
                $"Mobile template '{template.Id}' references unknown base '{template.BaseMobile}'."
            );
        }

        var resolvedBase = ResolveOne(baseTemplate, byId, visiting);
        visiting.Remove(template.Id);

        return Merge(resolvedBase, template);
    }

    private static MobileTemplate Merge(MobileTemplate baseTemplate, MobileTemplate derived)
    {
        var merged = new MobileTemplate
        {
            Id = derived.Id,
            Name = NonEmpty(derived.Name, baseTemplate.Name),
            Title = NonEmpty(derived.Title, baseTemplate.Title),
            Category = NonEmpty(derived.Category, baseTemplate.Category),
            Description = NonEmpty(derived.Description, baseTemplate.Description),
            BaseMobile = null,
            Strength = derived.Strength != DefaultStat ? derived.Strength : baseTemplate.Strength,
            Dexterity = derived.Dexterity != DefaultStat ? derived.Dexterity : baseTemplate.Dexterity,
            Intelligence = derived.Intelligence != DefaultStat ? derived.Intelligence : baseTemplate.Intelligence,
            LootTableId = derived.LootTableId ?? baseTemplate.LootTableId,
            BrainScript = derived.BrainScript ?? baseTemplate.BrainScript,
            Tags = baseTemplate.Tags.Concat(derived.Tags).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            Appearance = MergeAppearance(baseTemplate.Appearance, derived.Appearance),
            Equipment = derived.Equipment.Count > 0 ? derived.Equipment : baseTemplate.Equipment,
            Variants = derived.Variants.Count > 0 ? derived.Variants : baseTemplate.Variants
        };

        foreach (var (skill, value) in baseTemplate.Skills)
        {
            merged.Skills[skill] = value;
        }

        foreach (var (skill, value) in derived.Skills)
        {
            merged.Skills[skill] = value;
        }

        return merged;
    }

    private static MobileAppearance MergeAppearance(MobileAppearance baseAppearance, MobileAppearance derived)
        => new()
        {
            Body = derived.Body != 0 ? derived.Body : baseAppearance.Body,
            SkinHue = derived.SkinHue ?? baseAppearance.SkinHue,
            HairStyle = derived.HairStyle != 0 ? derived.HairStyle : baseAppearance.HairStyle,
            HairHue = derived.HairHue ?? baseAppearance.HairHue,
            FacialHairStyle = derived.FacialHairStyle != 0 ? derived.FacialHairStyle : baseAppearance.FacialHairStyle,
            FacialHairHue = derived.FacialHairHue ?? baseAppearance.FacialHairHue
        };

    private static string NonEmpty(string derived, string baseValue)
        => string.IsNullOrEmpty(derived) ? baseValue : derived;

    private const int DefaultStat = 50;
}
