using Moongate.UO.Data.Types;

namespace Moongate.UO.Data.Persistence.Entities;

/// <summary>
/// Represents a typed custom value persisted for an item.
/// </summary>
public sealed class ItemCustomProperty
{
    public ItemCustomPropertyType Type { get; set; }

    public long IntegerValue { get; set; }

    public bool BooleanValue { get; set; }

    public double DoubleValue { get; set; }

    public string? StringValue { get; set; }
}
