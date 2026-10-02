namespace Moongate.Server.Core.Data.Persistence;

/// <summary>
///     One file written by a SQL backup.
/// </summary>
public sealed class SqlBackupFile
{
    /// <summary>
    ///     Gets the database the file holds: <c>auth</c> or <c>world</c>.
    /// </summary>
    public required string Database { get; init; }

    /// <summary>
    ///     Gets the full path of the file.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    ///     Gets the size of the file in bytes.
    /// </summary>
    public required long Size { get; init; }
}
