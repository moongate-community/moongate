namespace Moongate.Server.Core.Data.Persistence;

/// <summary>
///     Controls the scheduled SQL backups and where their files go.
/// </summary>
public sealed class SqlBackupOptions
{
    /// <summary>
    ///     The longest interval the schedule's timer accepts, in minutes.
    /// </summary>
    public const int MaxIntervalMinutes = 71582;

    /// <summary>
    ///     Gets whether backups run on the schedule; a requested backup works either way.
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>
    ///     Gets the time between two scheduled backups.
    /// </summary>
    public TimeSpan Interval { get; init; } = TimeSpan.FromMinutes(1440);

    /// <summary>
    ///     Gets the absolute directory the files are written to.
    /// </summary>
    public string Directory { get; init; } = "backups";

    /// <summary>
    ///     Gets how many files are kept for each database.
    /// </summary>
    public int Keep { get; init; } = 5;

    /// <summary>
    ///     Rejects values the service cannot run with.
    /// </summary>
    public void Validate()
    {
        if (Interval < TimeSpan.FromMinutes(1) || Interval > TimeSpan.FromMinutes(MaxIntervalMinutes))
        {
            throw new ArgumentOutOfRangeException(
                nameof(Interval),
                $"The SQL backup interval must be between 1 and {MaxIntervalMinutes} minutes."
            );
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(Keep, 1);
        ArgumentException.ThrowIfNullOrWhiteSpace(Directory);
    }
}
