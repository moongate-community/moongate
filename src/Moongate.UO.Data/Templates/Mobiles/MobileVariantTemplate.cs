namespace Moongate.UO.Data.Templates.Mobiles;

/// <summary>
/// Defines a weighted mobile variant with appearance and equipment overrides.
/// </summary>
public class MobileVariantTemplate
{
    public string Name { get; set; }

    public int Weight { get; set; } = 1;

    public MobileAppearanceTemplate Appearance { get; set; } = new();

    public List<MobileEquipmentEntryTemplate> Equipment { get; set; } = [];
}
