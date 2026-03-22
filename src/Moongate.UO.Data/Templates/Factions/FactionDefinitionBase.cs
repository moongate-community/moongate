using System.Text.Json.Serialization;

namespace Moongate.UO.Data.Templates.Factions;

/// <summary>
/// Base DTO for polymorphic faction templates.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type"), JsonDerivedType(typeof(FactionDefinition), "faction")]
public abstract class FactionDefinitionBase
{
    public string Category { get; set; }

    public string Id { get; set; }

    public string Name { get; set; }

    public string Description { get; set; }

    public List<string> Tags { get; set; } = [];
}
