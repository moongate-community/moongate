using Moongate.Persistence.Data.Persistence;

namespace Moongate.Persistence.Interfaces.Persistence;

/// <summary>
/// Appends and replays journal entries from durable storage.
/// </summary>
public interface IJournalService
{
    /// <summary>
    /// Appends one journal entry to durable storage.
    /// </summary>
    ValueTask AppendAsync(JournalEntry entry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Appends multiple journal entries in a single batched write.
    /// </summary>
    ValueTask AppendBatchAsync(IReadOnlyList<JournalEntry> entries, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads all valid journal entries in persistence order.
    /// </summary>
    ValueTask<IReadOnlyCollection<JournalEntry>> ReadAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears the journal content after a successful snapshot.
    /// </summary>
    ValueTask ResetAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes journal entries up to and including the provided sequence id.
    /// </summary>
    ValueTask TrimThroughSequenceAsync(long inclusiveSequenceId, CancellationToken cancellationToken = default);
}
