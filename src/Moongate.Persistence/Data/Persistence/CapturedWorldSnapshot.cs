namespace Moongate.Persistence.Data.Persistence;

/// <summary>
/// Captures a consistent in-memory world snapshot plus the journal sequence boundary it includes.
/// </summary>
public sealed class CapturedWorldSnapshot
{
    public required WorldSnapshot Snapshot { get; init; }

    public required long CapturedLastSequenceId { get; init; }
}
