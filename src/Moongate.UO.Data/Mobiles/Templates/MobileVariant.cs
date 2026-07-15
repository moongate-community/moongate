namespace Moongate.UO.Data.Mobiles.Templates;

/// <summary>A weighted appearance/equipment variant chosen at spawn time.</summary>
public sealed class MobileVariant
{
    public string Name { get; set; } = string.Empty;

    public int Weight { get; set; } = 1;

    public MobileAppearance Appearance { get; set; } = new();

    public List<MobileEquipmentEntry> Equipment { get; set; } = [];
}
