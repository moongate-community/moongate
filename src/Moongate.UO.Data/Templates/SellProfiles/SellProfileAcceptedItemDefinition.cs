namespace Moongate.UO.Data.Templates.SellProfiles;

/// <summary>
/// Defines one item rule accepted by a vendor when buying from players.
/// </summary>
public class SellProfileAcceptedItemDefinition
{
    public string ItemTemplateId { get; set; }

    public List<string> Tags { get; set; } = [];

    public int Price { get; set; }

    public bool Enabled { get; set; } = true;
}
