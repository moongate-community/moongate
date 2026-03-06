namespace Moongate.UO.Data.Templates.Items;

/// <summary>
/// Defines a typed parameter mapped to item custom properties at spawn time.
/// </summary>
public sealed class ItemTemplateParamDefinition
{
    public ItemTemplateParamType Type { get; set; } = ItemTemplateParamType.String;

    public string Value { get; set; } = string.Empty;
}
