namespace Moongate.Persistence.Data.Internal;

internal sealed class PersistenceBackupCollection
{
    public required string Name { get; init; }

    public required PersistenceBackupFile Snapshot { get; init; }

    public required PersistenceBackupFile Journal { get; init; }
}
