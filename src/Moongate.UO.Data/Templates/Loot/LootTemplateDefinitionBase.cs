using System.Text.Json.Serialization;

namespace Moongate.UO.Data.Templates.Loot;

/// <summary>
/// Base DTO for polymorphic loot templates.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type"), JsonDerivedType(typeof(LootTemplateDefinition), "loot")]
public abstract class LootTemplateDefinitionBase
{
    public string Category { get; set; }

    public string Id { get; set; }

    public string Name { get; set; }

    public string Description { get; set; }
}
