using System.Text.Json.Serialization;

namespace Moongate.UO.Data.Templates.SellProfiles;

/// <summary>
/// Base DTO for polymorphic sell profile templates.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type"),
 JsonDerivedType(typeof(SellProfileTemplateDefinition), "sell_profile")]
public abstract class SellProfileTemplateDefinitionBase
{
    public string Category { get; set; }

    public string Id { get; set; }

    public string Name { get; set; }

    public string Description { get; set; }
}
