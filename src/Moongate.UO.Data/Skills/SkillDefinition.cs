using Moongate.UO.Data.Types;

namespace Moongate.UO.Data.Skills;

/// <summary>
/// The server-side rules for a single skill: how it scales on the character's stats, how fast
/// it gains, and which stats it trains. Authored in YAML (data/skills.yaml), not in code. This
/// is the balancing/definition layer, distinct from the client-file <c>Moongate.Ultima.Skill.SkillInfo</c>
/// (asset names) and from the per-character skill state (which lives on the mobile, added later).
/// </summary>
public sealed class SkillDefinition
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public double StrScale { get; set; }

    public double DexScale { get; set; }

    public double IntScale { get; set; }

    public double StrGain { get; set; }

    public double DexGain { get; set; }

    public double IntGain { get; set; }

    public double GainFactor { get; set; }

    public Stat PrimaryStat { get; set; }

    public Stat SecondaryStat { get; set; }
}
