using Moongate.UO.Data.Types;

namespace Moongate.UO.Data.Mobiles.Templates;

/// <summary>A weighted appearance/equipment variant chosen at spawn time.</summary>
public sealed class MobileVariant
{
    public string Name { get; set; } = string.Empty;

    public int Weight { get; set; } = 1;

    /// <summary>Gender for this variant; when null the template's gender is used.</summary>
    public MobileTemplateGenderType? Gender { get; set; }

    /// <summary>Loot table for this variant; when null the template's loot table is used.</summary>
    public string? LootTableId { get; set; }

    public MobileAppearance Appearance { get; set; } = new();

    public List<MobileEquipmentEntry> Equipment { get; set; } = [];
}
