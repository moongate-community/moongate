namespace Moongate.UO.Data.Templates.SellProfiles;

/// <summary>
/// Serializable definition for a vendor sell profile.
/// </summary>
public class SellProfileTemplateDefinition : SellProfileTemplateDefinitionBase
{
    public List<SellProfileVendorItemDefinition> VendorItems { get; set; } = [];

    public List<SellProfileAcceptedItemDefinition> AcceptedItems { get; set; } = [];
}
