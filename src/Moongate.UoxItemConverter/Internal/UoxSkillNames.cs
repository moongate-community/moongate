using Moongate.Ultima.Types;

namespace Moongate.UoxItemConverter.Internal;

/// <summary>
///     Maps a UOX3 skill tag, such as <c>MAGERY</c> or <c>MAGICRESISTANCE</c>, to its <see cref="SkillType" />.
/// </summary>
internal static class UoxSkillNames
{
    // Tags whose name is not the SkillType name without underscores.
    private static readonly Dictionary<string, SkillType> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["BLACKSMITHING"] = SkillType.Blacksmithy,
        ["BOWCRAFT"] = SkillType.BowcraftFletching,
        ["ENTICEMENT"] = SkillType.Discordance,
        ["EVALUATINGINTEL"] = SkillType.EvaluatingIntelligence,
        ["FORENSICS"] = SkillType.ForensicEvaluation,
        ["MAGICRESISTANCE"] = SkillType.ResistingSpells,
        ["TAMING"] = SkillType.AnimalTaming,
        ["TASTEID"] = SkillType.TasteIdentification,
        ["ITEMID"] = SkillType.ItemIdentification
    };

    private static readonly Dictionary<string, SkillType> ByName = Enum.GetValues<SkillType>()
                                                                        .ToDictionary(
                                                                            skill => skill.ToString(),
                                                                            skill => skill,
                                                                            StringComparer.OrdinalIgnoreCase
                                                                        );

    /// <summary>
    ///     Gets the skill a UOX3 tag sets; false for a tag that is not a skill.
    /// </summary>
    public static bool TryMap(string uoxTag, out SkillType skill)
    {
        return Aliases.TryGetValue(uoxTag, out skill) || ByName.TryGetValue(uoxTag, out skill);
    }
}
