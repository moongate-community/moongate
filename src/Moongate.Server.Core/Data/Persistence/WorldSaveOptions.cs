namespace Moongate.Server.Core.Data.Persistence;

/// <summary>Controls automatic world saves and completed backup retention.</summary>
public sealed class WorldSaveOptions
{
    public bool Enabled { get; init; } = true;
    public TimeSpan Interval { get; init; } = TimeSpan.FromSeconds(300);
    public bool BackupsEnabled { get; init; } = true;
    public int BackupRetentionCount { get; init; } = 5;

    /// <summary>Rejects invalid configuration before scheduling or persistence work begins.</summary>
    public void Validate()
    {
        if (Interval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(Interval), "The world-save interval must be positive.");
        }
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(BackupRetentionCount);
    }
}
