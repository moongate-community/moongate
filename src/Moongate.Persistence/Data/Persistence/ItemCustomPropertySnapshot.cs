using MessagePack;

namespace Moongate.Persistence.Data.Persistence;

/// <summary>
/// Serialized typed custom property for an item.
/// </summary>
[MessagePackObject(keyAsPropertyName: true)]
public sealed partial class ItemCustomPropertySnapshot
{
    public string Key { get; set; } = null!;

    public byte Type { get; set; }

    public long IntegerValue { get; set; }

    public bool BooleanValue { get; set; }

    public double DoubleValue { get; set; }

    public string? StringValue { get; set; }
}
