namespace Moongate.Persistence.Data.Internal;

internal sealed class PersistenceBackupManifest
{
    public required int FormatVersion { get; init; }

    public required PersistenceBackupCollection[] Collections { get; init; }
}
