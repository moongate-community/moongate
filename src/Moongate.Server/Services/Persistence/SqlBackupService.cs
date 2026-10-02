using System.Globalization;
using System.Text.RegularExpressions;
using Moongate.Persistence.Interfaces;
using Moongate.Persistence.Types.Persistence;
using Moongate.Server.Core.Data.Persistence;
using Moongate.Server.Core.Interfaces.Services;
using Serilog;

namespace Moongate.Server.Services.Persistence;

/// <summary>
///     Writes <c>auth_{date}.sql</c> and <c>world_{date}.sql</c> for the databases this process owns and keeps the
///     newest copies of each. It runs off the game loop on its own timer, so a login-only server backs up too.
/// </summary>
/// <remarks>
///     A file is written as <c>.tmp</c> and renamed when complete, so a file with the final name is always whole.
///     Only names this service writes are ever deleted.
/// </remarks>
public sealed partial class SqlBackupService : ISqlBackupService, IDisposable
{
    private const string TemporarySuffix = ".tmp";

    private readonly ILogger _logger = Log.ForContext<SqlBackupService>();

    private readonly IPersistenceDataExporter _exporter;

    private readonly SqlBackupOptions _options;

    private readonly TimeProvider _timeProvider;

    private readonly IWorldSaveService? _worldSave;

    private readonly CancellationTokenSource _stopping = new();

    private Task? _schedule;

    private Task<SqlBackupResult>? _active;

    private int _running;

    private bool _stopped;

    public SqlBackupService(
        IPersistenceDataExporter exporter,
        SqlBackupOptions options,
        TimeProvider timeProvider,
        IWorldSaveService? worldSave = null
    )
    {
        _exporter = exporter;
        _options = options;
        _timeProvider = timeProvider;
        _worldSave = worldSave;
    }

    /// <inheritdoc />
    public Task<SqlBackupResult> BackupAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.CompareExchange(ref _running, 1, 0) != 0)
        {
            return Task.FromResult(new SqlBackupResult { AlreadyRunning = true });
        }

        // Kept so a stop can wait for a backup that a command started.
        var run = RunGuardedAsync(cancellationToken);
        _active = run;

        return run;
    }

    /// <inheritdoc />
    public Task StartAsync()
    {
        _options.Validate();
        DeleteLeftovers();

        if (_options.Enabled)
        {
            // Created here and not in the loop: a tick is counted from the start, not from when the loop first runs.
            var timer = new PeriodicTimer(_options.Interval, _timeProvider);
            _schedule = Task.Run(() => RunScheduleAsync(timer, _stopping.Token));
        }

        _logger.Information(
            "SQL backup started: scheduled {Enabled}, interval {Interval}, keeping {Keep} copies in {Directory}",
            _options.Enabled,
            _options.Interval,
            _options.Keep,
            _options.Directory
        );

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync()
    {
        if (_stopped)
        {
            return;
        }

        _stopped = true;
        await _stopping.CancelAsync().ConfigureAwait(false);

        if (_schedule is not null)
        {
            await _schedule.ConfigureAwait(false);
        }

        var active = _active;

        if (active is not null)
        {
            try
            {
                await active.ConfigureAwait(false);
            }
            catch (Exception)
            {
                // The backup was canceled by this stop or failed for its caller, who gets the exception; here it
                // only matters that it has ended and removed its temporary file.
            }
        }
    }

    /// <summary>
    ///     Gets the file prefix of a database target.
    /// </summary>
    internal static string PrefixOf(PersistenceDatabaseTarget target)
    {
        return target == PersistenceDatabaseTarget.Accounts ? "auth" : "world";
    }

    [GeneratedRegex(@"^(auth|world)_\d{8}_\d{6}(_\d+)?\.sql(\.tmp)?$", RegexOptions.CultureInvariant)]
    private static partial Regex BackupFileName();

    private async Task<SqlBackupResult> BackupCoreAsync(CancellationToken cancellationToken)
    {
        var files = new List<SqlBackupFile>();
        var failures = new List<SqlBackupFailure>();

        if (_worldSave is not null)
        {
            try
            {
                await _worldSave.SaveAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.Error(exception, "SQL backup stopped: the world save before it failed");
                failures.Add(new() { Database = PrefixOf(PersistenceDatabaseTarget.Realm), Reason = exception.Message });

                return new() { Failures = failures };
            }
        }

        var stamp = _timeProvider.GetUtcNow().ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);

        foreach (var target in _exporter.ConfiguredTargets.Order())
        {
            var prefix = PrefixOf(target);
            var path = FreePath(prefix, stamp);
            var temporary = path + TemporarySuffix;

            try
            {
                Directory.CreateDirectory(_options.Directory);

                await using (var stream = new FileStream(
                                 temporary,
                                 FileMode.Create,
                                 FileAccess.Write,
                                 FileShare.None,
                                 81920,
                                 true
                             ))
                {
                    await _exporter.ExportDataAsync(target, stream, cancellationToken).ConfigureAwait(false);
                }

                File.Move(temporary, path, false);
                var file = new SqlBackupFile { Database = prefix, Path = path, Size = new FileInfo(path).Length };
                files.Add(file);
                _logger.Information("SQL backup wrote {Path} ({Size} bytes)", file.Path, file.Size);
                Rotate(prefix);
            }
            catch (Exception exception)
            {
                TryDelete(temporary);

                if (exception is OperationCanceledException)
                {
                    throw;
                }

                _logger.Error(exception, "SQL backup of {Database} failed", prefix);
                failures.Add(new() { Database = prefix, Reason = exception.Message });
            }
        }

        return new() { Files = files, Failures = failures };
    }

    // A second backup in the same second gets a numbered name, so it never replaces the first one.
    private string FreePath(string prefix, string stamp)
    {
        var path = Path.Combine(_options.Directory, $"{prefix}_{stamp}.sql");

        for (var copy = 2; File.Exists(path); copy++)
        {
            path = Path.Combine(_options.Directory, $"{prefix}_{stamp}_{copy}.sql");
        }

        return path;
    }

    private async Task<SqlBackupResult> RunGuardedAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _stopping.Token);

            return await BackupCoreAsync(linked.Token).ConfigureAwait(false);
        }
        finally
        {
            Volatile.Write(ref _running, 0);
        }
    }

    private void DeleteLeftovers()
    {
        try
        {
            if (!Directory.Exists(_options.Directory))
            {
                return;
            }

            foreach (var path in Directory.EnumerateFiles(_options.Directory, "*" + TemporarySuffix))
            {
                if (BackupFileName().IsMatch(Path.GetFileName(path)))
                {
                    TryDelete(path);
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.Warning(exception, "SQL backup cannot clean {Directory}", _options.Directory);
        }
    }

    private void Rotate(string prefix)
    {
        try
        {
            var old = Directory.EnumerateFiles(_options.Directory, prefix + "_*.sql")
                               .Where(path =>
                                   {
                                       var name = Path.GetFileName(path);

                                       return name.EndsWith(".sql", StringComparison.Ordinal) &&
                                              name.StartsWith(prefix + "_", StringComparison.Ordinal) &&
                                              BackupFileName().IsMatch(name);
                                   }
                               )
                               .OrderByDescending(Path.GetFileName, StringComparer.Ordinal)
                               .Skip(_options.Keep)
                               .ToArray();

            foreach (var path in old)
            {
                TryDelete(path);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.Warning(exception, "SQL backup cannot rotate the {Database} files", prefix);
        }
    }

    private async Task RunScheduleAsync(PeriodicTimer timer, CancellationToken cancellationToken)
    {
        using var owned = timer;

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                try
                {
                    var result = await BackupAsync(cancellationToken).ConfigureAwait(false);

                    if (result.AlreadyRunning)
                    {
                        _logger.Information("Scheduled SQL backup skipped: another backup is running");
                    }
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception exception)
                {
                    _logger.Error(exception, "Scheduled SQL backup failed");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // The server is stopping.
        }
    }

    private void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.Warning(exception, "SQL backup cannot delete {Path}", path);
        }
    }

    public void Dispose()
    {
        _stopping.Dispose();
    }
}
