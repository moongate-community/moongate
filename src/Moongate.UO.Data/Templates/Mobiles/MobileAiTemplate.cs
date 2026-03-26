namespace Moongate.UO.Data.Templates.Mobiles;

/// <summary>
/// Defines the canonical AI contract for a mobile template.
/// </summary>
public class MobileAiTemplate
{
    public string? Brain { get; set; }

    public string? FightMode { get; set; }

    public int? RangePerception { get; set; }

    public int? RangeFight { get; set; }
}
