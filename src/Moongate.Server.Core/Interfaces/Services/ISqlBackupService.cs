using Moongate.Server.Core.Data.Persistence;

namespace Moongate.Server.Core.Interfaces.Services;

/// <summary>
///     Writes rotating SQL exports of the databases this process owns, on a schedule and on request.
/// </summary>
public interface ISqlBackupService : IMoongateStartupService
{
    /// <summary>
    ///     Saves the world when this process runs one, then exports every configured database to its own file and
    ///     rotates the older files.
    /// </summary>
    /// <returns>
    ///     The written files and the databases that failed. A request made while another backup runs is refused
    ///     and says so in the result.
    /// </returns>
    /// <remarks>
    ///     A database that fails does not stop the others and is never thrown: it is in the result and in the log.
    ///     Only cancellation throws.
    /// </remarks>
    Task<SqlBackupResult> BackupAsync(CancellationToken cancellationToken = default);
}
