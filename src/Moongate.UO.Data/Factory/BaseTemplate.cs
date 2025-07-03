using System.Text.Json.Serialization;

namespace Moongate.UO.Data.Factory;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(ItemTemplate), "item")]
[JsonDerivedType(typeof(MobileTemplate), "mobile")]
public abstract class BaseTemplate
{
    public string Id { get; set; }

    public string? Name { get; set; }

    public string Category { get; set; }

    public string Description { get; set; }

    public string[] Tags { get; set; } = [];
}
