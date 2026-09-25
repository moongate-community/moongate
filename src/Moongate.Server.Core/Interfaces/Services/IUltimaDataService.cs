namespace Moongate.Server.Core.Interfaces.Services;

/// <summary>
///     Points the Ultima Online client-file loaders at the configured client directory.
/// </summary>
/// <remarks>
///     Starting resolves the configured path, including environment variables, and fails with
///     <see cref="DirectoryNotFoundException" /> when the directory is absent, since nothing that reads
///     client files can work without it. On success the client version found there is logged. Stopping
///     releases nothing, as the loaders open files on demand.
/// </remarks>
public interface IUltimaDataService : IMoongateStartupService
{
}
