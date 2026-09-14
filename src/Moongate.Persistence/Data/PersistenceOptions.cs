namespace Moongate.Persistence.Data;

/// <summary>Controls collection journal compaction and accepted payload size.</summary>
public sealed class PersistenceOptions
{
    public long JournalCheckpointThresholdBytes { get; init; } = 64L * 1024 * 1024;

    public int MaxPayloadBytes { get; init; } = 16 * 1024 * 1024;
}
