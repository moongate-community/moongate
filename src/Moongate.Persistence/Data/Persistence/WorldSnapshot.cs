using MemoryPack;

namespace Moongate.Persistence.Data.Persistence;

/// <summary>
/// Full persisted world state stored periodically on disk.
/// </summary>
[MemoryPackable(SerializeLayout.Explicit)]
public sealed partial class WorldSnapshot
{
    [MemoryPackOrder(0)]
    public int Version { get; set; } = 1;

    [MemoryPackOrder(1)]
    public long CreatedUnixMilliseconds { get; set; }

    [MemoryPackOrder(2)]
    public long LastSequenceId { get; set; }

    [MemoryPackOrder(3)]
    public EntitySnapshotBucket[] EntityBuckets { get; set; } = [];
}
