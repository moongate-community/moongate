using MemoryPack;
using Moongate.UO.Data.Types;

namespace Moongate.UO.Data.Persistence.Entities;

/// <summary>
/// Represents a typed custom value persisted for an item.
/// </summary>
[MemoryPackable(SerializeLayout.Explicit)]
public sealed partial class ItemCustomProperty
{
    [MemoryPackOrder(0)]
    public ItemCustomPropertyType Type { get; set; }

    [MemoryPackOrder(1)]
    public long IntegerValue { get; set; }

    [MemoryPackOrder(2)]
    public bool BooleanValue { get; set; }

    [MemoryPackOrder(3)]
    public double DoubleValue { get; set; }

    [MemoryPackOrder(4)]
    public string? StringValue { get; set; }
}
