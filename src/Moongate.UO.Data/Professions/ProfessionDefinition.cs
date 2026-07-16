namespace Moongate.UO.Data.Professions;

/// <summary>
/// A character-creation profession preset: display/gump ids plus the starting skills and stats.
/// </summary>
public sealed class ProfessionDefinition
{
    public string Name { get; set; } = string.Empty;
    public string TrueName { get; set; } = string.Empty;
    public int NameId { get; set; }
    public int DescId { get; set; }
    public int Desc { get; set; }
    public bool TopLevel { get; set; }
    public int Gump { get; set; }
    public string Type { get; set; } = string.Empty;
    public List<ProfessionSkill> Skills { get; set; } = [];
    public List<ProfessionStat> Stats { get; set; } = [];
}
