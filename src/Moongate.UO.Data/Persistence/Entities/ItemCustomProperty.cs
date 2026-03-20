using Moongate.Generators.Annotations.Persistence;
using Moongate.UO.Data.Types;

namespace Moongate.UO.Data.Persistence.Entities;

/// <summary>
/// Represents a typed custom value persisted for an item.
/// </summary>
[MoongatePersistedEntity]
public sealed class ItemCustomProperty
{
    [MoongatePersistedMember(0)]
    public ItemCustomPropertyType Type { get; set; }

    [MoongatePersistedMember(1)]
    public long IntegerValue { get; set; }

    [MoongatePersistedMember(2)]
    public bool BooleanValue { get; set; }

    [MoongatePersistedMember(3)]
    public double DoubleValue { get; set; }

    [MoongatePersistedMember(4)]
    public string? StringValue { get; set; }
}
