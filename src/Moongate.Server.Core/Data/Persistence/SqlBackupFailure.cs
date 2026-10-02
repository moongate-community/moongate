namespace Moongate.Server.Core.Data.Persistence;

/// <summary>
///     One database a SQL backup could not write.
/// </summary>
public sealed class SqlBackupFailure
{
    /// <summary>
    ///     Gets the database that failed: <c>auth</c> or <c>world</c>.
    /// </summary>
    public required string Database { get; init; }

    /// <summary>
    ///     Gets a one-line reason; the details are in the log.
    /// </summary>
    public required string Reason { get; init; }
}
