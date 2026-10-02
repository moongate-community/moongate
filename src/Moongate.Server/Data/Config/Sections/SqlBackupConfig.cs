using Moongate.Core.Extensions.Env;
using Moongate.Server.Core.Data.Persistence;

namespace Moongate.Server.Data.Config.Sections;

/// <summary>
///     TOML settings for the rotating SQL backups.
/// </summary>
public sealed class SqlBackupConfig
{
    public bool Enabled { get; set; }

    public int IntervalMinutes { get; set; } = 1440;

    public string Directory { get; set; } = "backups";

    public int Keep { get; set; } = 5;

    /// <summary>
    ///     Maps validated TOML settings to the service's immutable options; a relative directory is resolved
    ///     against the server root.
    /// </summary>
    public SqlBackupOptions ToOptions(string rootDirectory)
    {
        Validate();
        var options = new SqlBackupOptions
        {
            Enabled = Enabled,
            Interval = TimeSpan.FromMinutes(IntervalMinutes),
            Directory = Path.GetFullPath(Path.Combine(rootDirectory, Directory.ExpandEnvironmentVariables(true))),
            Keep = Keep
        };
        options.Validate();

        return options;
    }

    /// <summary>
    ///     Rejects invalid values even when scheduled backups are disabled.
    /// </summary>
    public void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(IntervalMinutes);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(IntervalMinutes, SqlBackupOptions.MaxIntervalMinutes);
        ArgumentOutOfRangeException.ThrowIfLessThan(Keep, 1);
        ArgumentException.ThrowIfNullOrWhiteSpace(Directory);
    }
}
