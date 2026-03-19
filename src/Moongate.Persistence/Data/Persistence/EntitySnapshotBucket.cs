using MessagePack;

namespace Moongate.Persistence.Data.Persistence;

/// <summary>
/// Serialized snapshot bucket for a single registered entity type.
/// </summary>
[MessagePackObject(true)]
public sealed class EntitySnapshotBucket
{
    public ushort TypeId { get; set; }

    public string TypeName { get; set; } = string.Empty;

    public int SchemaVersion { get; set; }

    public byte[] Payload { get; set; } = [];
}
