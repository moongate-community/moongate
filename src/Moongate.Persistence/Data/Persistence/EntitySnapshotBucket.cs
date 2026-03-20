using MemoryPack;

namespace Moongate.Persistence.Data.Persistence;

/// <summary>
/// Serialized snapshot bucket for a single registered entity type.
/// </summary>
[MemoryPackable(SerializeLayout.Explicit)]
public sealed partial class EntitySnapshotBucket
{
    [MemoryPackOrder(0)]
    public ushort TypeId { get; set; }

    [MemoryPackOrder(1)]
    public string TypeName { get; set; } = string.Empty;

    [MemoryPackOrder(2)]
    public int SchemaVersion { get; set; }

    [MemoryPackOrder(3)]
    public byte[] Payload { get; set; } = [];
}
