using MessagePack;

namespace Moongate.Persistence.Data.Persistence;

/// <summary>
/// Full persisted world state stored periodically on disk.
/// </summary>
[MessagePackObject(true)]
public sealed class WorldSnapshot
{
    public int Version { get; set; } = 1;

    public long CreatedUnixMilliseconds { get; set; }

    public long LastSequenceId { get; set; }

    public EntitySnapshotBucket[] EntityBuckets { get; set; } = [];
}
