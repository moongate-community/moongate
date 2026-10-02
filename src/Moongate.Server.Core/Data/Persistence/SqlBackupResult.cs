namespace Moongate.Server.Core.Data.Persistence;

/// <summary>
///     What one SQL backup run did.
/// </summary>
public sealed class SqlBackupResult
{
    /// <summary>
    ///     Gets whether the request was refused because another backup was running.
    /// </summary>
    public bool AlreadyRunning { get; init; }

    /// <summary>
    ///     Gets the files that were written.
    /// </summary>
    public IReadOnlyList<SqlBackupFile> Files { get; init; } = [];

    /// <summary>
    ///     Gets the databases that could not be written.
    /// </summary>
    public IReadOnlyList<SqlBackupFailure> Failures { get; init; } = [];
}
