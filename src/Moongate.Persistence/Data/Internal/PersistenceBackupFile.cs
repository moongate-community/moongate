namespace Moongate.Persistence.Data.Internal;

internal sealed class PersistenceBackupFile
{
    public required string FileName { get; init; }

    public required long Length { get; init; }

    public required string Sha256 { get; init; }
}
