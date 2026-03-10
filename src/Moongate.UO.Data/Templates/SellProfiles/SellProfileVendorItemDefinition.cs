namespace Moongate.UO.Data.Templates.SellProfiles;

/// <summary>
/// Defines one item entry that the vendor sells to players.
/// </summary>
public class SellProfileVendorItemDefinition
{
    public string ItemTemplateId { get; set; }

    public int Price { get; set; }

    public int MaxStock { get; set; }

    public bool Enabled { get; set; } = true;
}
