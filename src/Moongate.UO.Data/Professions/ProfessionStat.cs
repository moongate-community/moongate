using Moongate.UO.Data.Types;

namespace Moongate.UO.Data.Professions;

/// <summary>A starting-stat grant on a profession preset.</summary>
public sealed class ProfessionStat
{
    public StatType Type { get; set; }
    public int Value { get; set; }
}
