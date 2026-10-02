using Moongate.Server.Core.Data.Persistence;
using Moongate.Server.Core.Interfaces.Services;

namespace Moongate.Tests.TestSupport.Persistence;

/// <summary>
///     Returns a fixed result and counts the requests.
/// </summary>
public sealed class StubSqlBackupService : ISqlBackupService
{
    public SqlBackupResult Result { get; set; } = new();

    public int Calls { get; private set; }

    public Task<SqlBackupResult> BackupAsync(CancellationToken cancellationToken = default)
    {
        Calls++;

        return Task.FromResult(Result);
    }

    public Task StartAsync()
    {
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        return Task.CompletedTask;
    }
}
