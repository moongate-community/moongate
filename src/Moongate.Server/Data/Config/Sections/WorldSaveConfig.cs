using Moongate.Server.Core.Data.Persistence;

namespace Moongate.Server.Data.Config.Sections;

/// <summary>TOML settings for periodic saves and backup retention.</summary>
public sealed class WorldSaveConfig
{
    public bool Enabled { get; set; } = true;
    public int IntervalSeconds { get; set; } = 300;
    public bool BackupsEnabled { get; set; } = true;
    public int BackupRetentionCount { get; set; } = 5;

    /// <summary>Maps validated TOML settings to the service's immutable options.</summary>
    /// <remarks>The backup directory is not configured here: the service derives it from the root.</remarks>
    public WorldSaveOptions ToOptions()
    {
        Validate();
        var options = new WorldSaveOptions
        {
            Enabled = Enabled,
            Interval = TimeSpan.FromSeconds(IntervalSeconds),
            BackupsEnabled = BackupsEnabled,
            BackupRetentionCount = BackupRetentionCount
        };
        options.Validate();
        return options;
    }

    /// <summary>Rejects invalid intervals and retention even when automatic saving is disabled.</summary>
    public void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(IntervalSeconds);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(BackupRetentionCount);
    }
}
