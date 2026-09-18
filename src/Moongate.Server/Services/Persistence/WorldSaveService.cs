using System.Globalization;
using System.Runtime.ExceptionServices;
using Moongate.Core.Directories;
using Moongate.Persistence.Services;
using Moongate.Server.Core.Data.Persistence;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Services.Persistence.Internal;
using Serilog;

namespace Moongate.Server.Services.Persistence;

/// <summary>Coordinates periodic and terminal captures with durable world persistence.</summary>
public sealed class WorldSaveService : IWorldSaveService
{
    private readonly Lock _gate = new();
    private readonly MoongatePersistenceService _persistence;
    private readonly IGameLoopService _gameLoop;
    private readonly ITimerService _timers;
    private readonly WorldSaveOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly string _backupDirectory;
    private readonly ILogger _logger = Log.ForContext<WorldSaveService>();
    private Task? _activeSave;
    private Task? _stopTask;
    private string? _timerId;
    private bool _started;
    private bool _activated;
    private bool _stopping;
    private bool _saveFinal;

    public const int StartupPriority = 40;

    public WorldSaveService(
        MoongatePersistenceService persistence,
        IGameLoopService gameLoop,
        ITimerService timers,
        WorldSaveOptions options,
        TimeProvider timeProvider,
        DirectoriesConfig directoriesConfig
    )
    {
        _persistence = persistence;
        _gameLoop = gameLoop;
        _timers = timers;
        _options = options;
        _timeProvider = timeProvider;

        // Derived from the root rather than configured: backups live beside the save data they mirror.
        _backupDirectory = Path.Join(directoriesConfig.Root, "world-saves");
    }

    /// <inheritdoc />
    public Task StartAsync()
    {
        lock (_gate)
        {
            if (_stopping)
            {
                throw new InvalidOperationException("World saving cannot restart after shutdown.");
            }
            _options.Validate();
            _started = true;
            _logger.Information(
                "World saving started: autosave {Enabled}, interval {Interval}, backups {BackupsEnabled}, retention {Retention}, directory {BackupDirectory}",
                _options.Enabled,
                _options.Interval,
                _options.BackupsEnabled,
                _options.BackupRetentionCount,
                _backupDirectory
            );

            return Task.CompletedTask;
        }
    }

    /// <inheritdoc />
    public void Activate()
    {
        lock (_gate)
        {
            if (!_started || _stopping)
            {
                throw new InvalidOperationException("World saving must be started before activation.");
            }

            if (_activated)
            {
                return;
            }

            if (_options.Enabled)
            {
                _timerId = _timers.RegisterTimer("world-save", _options.Interval, RequestAutosave, repeat: true);
            }
            _activated = true;
        }
    }

    /// <inheritdoc />
    public Task SaveAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            if (!_activated || _stopping)
            {
                throw new InvalidOperationException("World saving is not active or shutdown has begun.");
            }

            return RequestSaveLocked().WaitAsync(cancellationToken);
        }
    }

    /// <summary>Drains active saving and the loop without publishing a final capture.</summary>
    public Task StopAsync()
        => StopAsync(saveFinal: false);

    /// <inheritdoc />
    public Task StopAsync(bool saveFinal)
    {
        lock (_gate)
        {
            if (_stopTask is not null)
            {
                if (saveFinal && !_saveFinal)
                {
                    throw new InvalidOperationException("A cleanup-only stop cannot be upgraded to a final world save.");
                }

                return _stopTask;
            }
            _stopping = true;
            _saveFinal = saveFinal;
            _stopTask = StopCoreAsync(saveFinal && _activated);

            return _stopTask;
        }
    }

    private void RequestAutosave()
    {
        lock (_gate)
        {
            if (_activated && !_stopping)
            {
                RequestSaveLocked();
            }
        }
    }

    private Task RequestSaveLocked()
    {
        if (_activeSave is null || _activeSave.IsCompleted)
        {
            // One supervised worker per active save, never one worker per entity or timer tick.
            _activeSave = Task.Run(() => SaveCoreAsync(finalSave: false));
            _ = ObserveSaveAsync(_activeSave);
        }

        return _activeSave;
    }

    private static async Task ObserveSaveAsync(Task saving)
    {
        try
        {
            await saving.ConfigureAwait(false);
        }
        catch (Exception)
        {
            // SaveCoreAsync logs the failure. Keep the original faulted task for callers and shutdown.
        }
    }

    private async Task SaveCoreAsync(bool finalSave)
    {
        var startedAt = _timeProvider.GetTimestamp();
        string? backupDirectory = null;

        try
        {
            _logger.Information("Starting world save; final capture {FinalSave}", finalSave);

            if (_options.BackupsEnabled)
            {
                backupDirectory = CreateBackupDirectory();
                await _persistence.SaveAllWithBackupAsync(
                                      (capture, _) => CaptureAsync(capture, finalSave),
                                      backupDirectory,
                                      CancellationToken.None
                                  )
                                  .ConfigureAwait(false);
                PruneBackups(backupDirectory);
            }
            else
            {
                await _persistence.SaveAllAsync((capture, _) => CaptureAsync(capture, finalSave), CancellationToken.None)
                                  .ConfigureAwait(false);
            }
            _logger.Information(
                "World save completed in {ElapsedMilliseconds:F2} ms; backup {BackupDirectory}",
                _timeProvider.GetElapsedTime(startedAt).TotalMilliseconds,
                backupDirectory
            );
        }
        catch (Exception exception)
        {
            _logger.Error(
                exception,
                "World save failed after {ElapsedMilliseconds:F2} ms; backup {BackupDirectory}",
                _timeProvider.GetElapsedTime(startedAt).TotalMilliseconds,
                backupDirectory
            );

            throw;
        }
    }

    private async Task CaptureAsync(Action capture, bool finalSave)
    {
        var item = new WorldSaveCaptureWorkItem(
            () =>
            {
                var startedAt = _timeProvider.GetTimestamp();
                capture();
                _logger.Debug(
                    "World capture completed in {ElapsedMilliseconds:F2} ms",
                    _timeProvider.GetElapsedTime(startedAt).TotalMilliseconds
                );
            }
        );

        if (finalSave)
        {
            await _gameLoop.StopAsync(item).ConfigureAwait(false);
        }
        else
        {
            try
            {
                await _gameLoop.PostAsync(item).ConfigureAwait(false);
            }
            catch (InvalidOperationException) when (_gameLoop.Completion.IsCompleted)
            {
                await _gameLoop.Completion.ConfigureAwait(false);

                throw;
            }
            await Task.WhenAny(item.Completion, _gameLoop.Completion).ConfigureAwait(false);

            if (!item.Completion.IsCompleted)
            {
                await _gameLoop.Completion.ConfigureAwait(false);

                throw new InvalidOperationException("The game loop stopped before the world capture executed.");
            }
        }
        await item.Completion.ConfigureAwait(false);
    }

    private async Task StopCoreAsync(bool saveFinal)
    {
        List<Exception> failures = [];

        try
        {
            if (_timerId is not null)
            {
                _timers.UnregisterTimer(_timerId);
                _timerId = null;
            }
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }

        if (_activeSave is not null)
        {
            await CaptureFailureAsync(() => _activeSave, failures).ConfigureAwait(false);
        }

        if (saveFinal)
        {
            await CaptureFailureAsync(() => SaveCoreAsync(finalSave: true), failures).ConfigureAwait(false);
        }

        // Persistence may fail before calling the capture delegate. Always drain the loop before world owners stop.
        await CaptureFailureAsync(_gameLoop.StopAsync, failures).ConfigureAwait(false);

        if (failures.Count == 1)
        {
            ExceptionDispatchInfo.Capture(failures[0]).Throw();
        }

        if (failures.Count > 1)
        {
            throw new AggregateException(failures);
        }
    }

    private static async Task CaptureFailureAsync(Func<Task> operation, List<Exception> failures)
    {
        try
        {
            await operation().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            if (!failures.Any(failure => ReferenceEquals(failure, exception)))
            {
                failures.Add(exception);
            }
        }
    }

    private string CreateBackupDirectory()
    {
        var timestamp = _timeProvider.GetUtcNow();

        if (Directory.Exists(_backupDirectory))
        {
            var latest = Directory.EnumerateDirectories(_backupDirectory)
                                  .Where(IsCompletedBackup)
                                  .Select(Path.GetFileName)
                                  .OrderDescending(StringComparer.Ordinal)
                                  .FirstOrDefault();

            if (latest is not null)
            {
                var previous = DateTimeOffset.ParseExact(
                    latest.AsSpan(11, 23),
                    "yyyyMMddTHHmmssfffffffZ",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal
                );

                if (timestamp <= previous)
                {
                    // Names remain ordered by publication even after a clock rollback or a service restart.
                    timestamp = previous.AddTicks(1);
                }
            }
        }
        var stamp = timestamp.ToString("yyyyMMddTHHmmssfffffffZ", CultureInfo.InvariantCulture);

        return Path.Combine(_backupDirectory, $"world-save-{stamp}-{Guid.NewGuid():N}");
    }

    private void PruneBackups(string newestDirectory)
    {
        var completed = Directory.EnumerateDirectories(_backupDirectory)
                                 .Where(IsCompletedBackup)
                                 .Where(path => !string.Equals(path, newestDirectory, StringComparison.Ordinal))
                                 .OrderByDescending(Path.GetFileName, StringComparer.Ordinal)
                                 .Skip(_options.BackupRetentionCount - 1);

        foreach (var directory in completed)
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static bool IsCompletedBackup(string directory)
    {
        var name = Path.GetFileName(directory);
        const string prefix = "world-save-";
        const string timestampFormat = "yyyyMMddTHHmmssfffffffZ";

        return name.Length == prefix.Length + 23 + 1 + 32 &&
               name.StartsWith(prefix, StringComparison.Ordinal) &&
               name[prefix.Length + 23] == '-' &&
               DateTimeOffset.TryParseExact(
                   name.AsSpan(prefix.Length, 23),
                   timestampFormat,
                   CultureInfo.InvariantCulture,
                   DateTimeStyles.AssumeUniversal,
                   out _
               ) &&
               Guid.TryParseExact(name.AsSpan(prefix.Length + 24), "N", out _) &&
               File.Exists(Path.Combine(directory, MoongatePersistenceBackup.ManifestFileName));
    }
}
