namespace Moongate.Persistence.Data.Persistence;

/// <summary>
/// Defines file paths used by snapshot and journal persistence.
/// </summary>
public sealed class PersistenceOptions
{
    public PersistenceOptions(string snapshotFilePath, string journalFilePath, bool enableFileLock = true)
    {
        SnapshotFilePath = snapshotFilePath;
        JournalFilePath = journalFilePath;
        EnableFileLock = enableFileLock;
    }

    public string SnapshotFilePath { get; }

    public string JournalFilePath { get; }

    public bool EnableFileLock { get; }
}
