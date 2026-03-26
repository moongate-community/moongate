namespace Moongate.UO.Data.Templates.Mobiles;

/// <summary>
/// Defines the canonical AI contract for a mobile template.
/// </summary>
public class MobileAiTemplate
{
    public string Brain { get; set; } = "none";

    public string FightMode { get; set; } = "closest";

    public int RangePerception { get; set; } = 16;

    public int RangeFight { get; set; } = 1;
}
