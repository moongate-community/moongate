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
    public WorldSaveOptions ToOptions(string backupDirectory)
    {
        Validate();
        var options = new WorldSaveOptions
        {
            Enabled = Enabled,
            Interval = TimeSpan.FromSeconds(IntervalSeconds),
            BackupsEnabled = BackupsEnabled,
            BackupRetentionCount = BackupRetentionCount,
            BackupDirectory = backupDirectory
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
