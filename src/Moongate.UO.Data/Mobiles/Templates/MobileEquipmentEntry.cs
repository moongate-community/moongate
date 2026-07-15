namespace Moongate.UO.Data.Mobiles.Templates;

/// <summary>An item to equip on a spawned mobile: item template id, layer name, optional hue spec.</summary>
public sealed class MobileEquipmentEntry
{
    public string Item { get; set; } = string.Empty;

    public string Layer { get; set; } = string.Empty;

    public string? Hue { get; set; }
}
