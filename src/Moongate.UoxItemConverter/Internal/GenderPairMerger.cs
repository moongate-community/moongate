using System.Reflection;
using Moongate.Core.Utils;
using Moongate.Server.Ultima.Data.Templates.Mobiles;
using Moongate.Server.Ultima.Types.Mobiles;
using Moongate.Ultima.Types;

namespace Moongate.UoxItemConverter.Internal;

/// <summary>
///     Merges a UOX3 <c>GET=m_x f_x</c> pair into one template with <c>gender = "random"</c>: what both set alike is
///     kept, equipment only one of them wears is filtered by gender, and any other difference takes the male value
///     and is reported.
/// </summary>
internal static class GenderPairMerger
{
    // Set by the merge itself, or compared on their own.
    private static readonly HashSet<string> MergedProperties =
    [
        nameof(MobileTemplate.Id), nameof(MobileTemplate.BaseId), nameof(MobileTemplate.Gender),
        nameof(MobileTemplate.NameList), nameof(MobileTemplate.Equipment), nameof(MobileTemplate.Sounds)
    ];

    public static bool TryMerge(
        string id,
        MobileTemplate first,
        MobileTemplate second,
        Func<MobileTemplate, (RaceType? Race, MobileGenderType? Gender)> resolve,
        ConversionReport report,
        out MobileTemplate merged
    )
    {
        merged = null!;
        var (firstRace, firstGender) = resolve(first);
        var (secondRace, secondGender) = resolve(second);

        if (firstRace is null || firstRace != secondRace ||
            (firstGender, secondGender) is not ((MobileGenderType.Male, MobileGenderType.Female) or
                                             (MobileGenderType.Female, MobileGenderType.Male)))
        {
            report.Count("two-target get, not a gender pair");

            return false;
        }

        var (male, female) = firstGender == MobileGenderType.Male ? (first, second) : (second, first);

        merged = Copy(male);
        merged.Id = id;
        merged.Gender = MobileGenderType.Random;
        merged.Race = firstRace;

        if (male.BaseId != female.BaseId)
        {
            report.Count("base differs between the male and female");
        }

        merged.NameList = (male.NameList, female.NameList) switch
        {
            ("male", "female") => "{gender}",
            var (maleList, femaleList) when maleList == femaleList => maleList,
            _ => Differs("name_list", male.NameList, report)
        };

        foreach (var property in typeof(MobileTemplate).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!MergedProperties.Contains(property.Name) && Describe(property, male) != Describe(property, female))
            {
                report.Count($"field differs between the male and female: {StringUtils.ToSnakeCase(property.Name)}");
            }
        }

        // A male sound on a female mobile is wrong, not just imprecise (humans die with gendered screams): when the
        // two differ, neither is kept.
        var soundsProperty = typeof(MobileTemplate).GetProperty(nameof(MobileTemplate.Sounds))!;

        if (Describe(soundsProperty, male) != Describe(soundsProperty, female))
        {
            merged.Sounds = null;
            report.Count("sounds differ between the male and female, left unset");
        }

        merged.Equipment = MergeEquipment(male.Equipment ?? [], female.Equipment ?? []);

        return true;
    }

    private static string? Differs(string field, string? maleValue, ConversionReport report)
    {
        report.Count($"field differs between the male and female: {field}");

        return maleValue;
    }

    // Shared entries first, unfiltered; then the male-only and the female-only ones, filtered.
    private static List<MobileEquipmentEntry>? MergeEquipment(
        List<MobileEquipmentEntry> male,
        List<MobileEquipmentEntry> female
    )
    {
        var femaleKeys = female.Select(Key).ToList();
        var maleKeys = male.Select(Key).ToList();
        var merged = male.Where(entry => femaleKeys.Contains(Key(entry)))
                         .Select(entry => new MobileEquipmentEntry { Items = entry.Items, Hue = entry.Hue })
                         .ToList();

        merged.AddRange(
            male.Where(entry => !femaleKeys.Contains(Key(entry)))
                .Select(entry => new MobileEquipmentEntry { Items = entry.Items, Hue = entry.Hue, Gender = GenderType.Male })
        );
        merged.AddRange(
            female.Where(entry => !maleKeys.Contains(Key(entry)))
                  .Select(entry => new MobileEquipmentEntry { Items = entry.Items, Hue = entry.Hue, Gender = GenderType.Female })
        );

        return merged.Count == 0 ? null : merged;
    }

    private static string Key(MobileEquipmentEntry entry)
    {
        return $"{string.Join("|", entry.Items)}#{entry.Hue}";
    }

    // The TOML a template with only this property set would have: an exact comparison of any property type.
    private static string Describe(PropertyInfo property, MobileTemplate template)
    {
        var probe = new MobileTemplate { Id = "probe" };
        property.SetValue(probe, property.GetValue(template));

        return TomlUtils.Serialize(probe);
    }

    private static MobileTemplate Copy(MobileTemplate template)
    {
        return TomlUtils.Deserialize<MobileTemplate>(TomlUtils.Serialize(template))!;
    }
}
