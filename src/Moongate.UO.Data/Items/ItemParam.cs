namespace Moongate.UO.Data.Items;

/// <summary>A typed script parameter attached to an item template (name maps to type + value).</summary>
public sealed class ItemParam
{
    public string Type { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
