using System.Diagnostics;
using Moongate.Core.Data.Directories;
using Moongate.Core.Types;
using Moongate.Persistence.Data.Persistence;
using Moongate.Persistence.Interfaces.Persistence;
using Moongate.Persistence.Services.Persistence;
using Moongate.Server.Data.Config;
using Moongate.Server.Data.Events.Persistence;
using Moongate.Server.Interfaces.Services.EvenLoop;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Metrics;
using Moongate.Server.Interfaces.Services.Persistence;
using Moongate.Server.Interfaces.Services.Timing;
using Moongate.Server.Metrics.Data;
using Moongate.Server.Services.EventLoop;
using Serilog;

namespace Moongate.Server.Services.Persistence;

/// <summary>
/// Wraps persistence unit-of-work lifecycle for host-managed startup and shutdown.
/// </summary>
public sealed class PersistenceService : IPersistenceService, IPersistenceMetricsSource
{
    private readonly ILogger _logger = Log.ForContext<PersistenceService>();
    private readonly DirectoriesConfig _directoriesConfig;
    private readonly Lock _metricsSync = new();
    private PersistenceMetricsSnapshot _metricsSnapshot = new(0, 0, null, 0);

    private readonly IGameEventBusService _gameEventBusService;
    private readonly IBackgroundJobService _backgroundJobService;
    private readonly ITimerService _timerService;
    private readonly MoongatePersistenceConfig _persistenceConfig;
    private string? _dbSaveTimerId;
    private int _autosaveInFlight;

    public PersistenceService(
        DirectoriesConfig directoriesConfig,
        ITimerService timerService,
        MoongateConfig moongateConfig,
        IGameEventBusService gameEventBusService
    )
        : this(directoriesConfig, timerService, new BackgroundJobService(), moongateConfig, gameEventBusService) { }

    public PersistenceService(
        DirectoriesConfig directoriesConfig,
        ITimerService timerService,
        IBackgroundJobService backgroundJobService,
        MoongateConfig moongateConfig,
        IGameEventBusService gameEventBusService
    )
    {
        ArgumentNullException.ThrowIfNull(directoriesConfig);
        ArgumentNullException.ThrowIfNull(timerService);
        ArgumentNullException.ThrowIfNull(backgroundJobService);
        ArgumentNullException.ThrowIfNull(moongateConfig);
        ArgumentNullException.ThrowIfNull(moongateConfig.Persistence);

        _directoriesConfig = directoriesConfig;
        _timerService = timerService;
        _backgroundJobService = backgroundJobService;
        _gameEventBusService = gameEventBusService;
        _persistenceConfig = moongateConfig.Persistence;

        var saveDirectory = directoriesConfig[DirectoryType.Save];
        var options = new PersistenceOptions(
            Path.Combine(saveDirectory, "world.snapshot.bin"),
            Path.Combine(saveDirectory, "world.journal.bin")
        );

        UnitOfWork = new PersistenceUnitOfWork(options);
    }

    public IPersistenceUnitOfWork UnitOfWork { get; }

    public void Dispose()
    {
        if (UnitOfWork is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    public PersistenceMetricsSnapshot GetMetricsSnapshot()
    {
        lock (_metricsSync)
        {
            return _metricsSnapshot;
        }
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
        => await RunSaveAsync("Persistence service save", cancellationToken);

    public async Task StartAsync()
    {
        _logger.Verbose("Persistence service start requested");
        await UnitOfWork.InitializeAsync();

        _dbSaveTimerId ??= _timerService.RegisterTimer(
            "db_save",
            TimeSpan.FromSeconds(Math.Max(1, _persistenceConfig.SaveIntervalSeconds)),
            () =>
            {
                if (Interlocked.CompareExchange(ref _autosaveInFlight, 1, 0) != 0)
                {
                    _logger.Debug("Automatic DB save skipped because a previous autosave is still running.");

                    return;
                }

                _backgroundJobService.EnqueueBackground(RunAutomaticSaveAsync);
            },
            repeat: true
        );

        _logger.Verbose("Persistence service start completed");
        _logger.Information(
            "Persistence service started in directory: {SaveDirectory}",
            _directoriesConfig[DirectoryType.Save]
        );
    }

    public async Task StopAsync()
    {
        _logger.Verbose("Persistence service stop requested");

        if (!string.IsNullOrWhiteSpace(_dbSaveTimerId))
        {
            _timerService.UnregisterTimer(_dbSaveTimerId);
            _dbSaveTimerId = null;
        }

        await RunSaveAsync("Persistence service stop-save");
        _logger.Verbose("Persistence service stop completed");
    }

    private async Task RunAutomaticSaveAsync()
    {
        try
        {
            await RunSaveAsync("Automatic DB save");
            _logger.Debug("Automatic DB save completed in {ElapsedMs} ms", GetMetricsSnapshot().LastSaveDurationMs);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Automatic DB save timer failed.");
        }
        finally
        {
            Interlocked.Exchange(ref _autosaveInFlight, 0);
        }
    }

    private async Task RunSaveAsync(string operationName, CancellationToken cancellationToken = default)
    {
        await _gameEventBusService.PublishAsync(new DatabaseSavingStartEvent(), cancellationToken);

        _logger.Verbose("{OperationName} requested", operationName);

        await SaveSnapshotWithMetricsAsync(cancellationToken);

        _logger.Verbose(
            "{OperationName} completed in {ElapsedMs} ms (TotalSaves={TotalSaves}, SaveErrors={SaveErrors})",
            operationName,
            GetMetricsSnapshot().LastSaveDurationMs,
            GetMetricsSnapshot().TotalSaves,
            GetMetricsSnapshot().SaveErrors
        );
        await _gameEventBusService.PublishAsync(
            new DatabaseSavedEvent(GetMetricsSnapshot().LastSaveDurationMs),
            cancellationToken
        );
    }

    private async Task SaveSnapshotWithMetricsAsync(CancellationToken cancellationToken = default)
    {
        var start = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var captured = await UnitOfWork.CaptureSnapshotAsync(cancellationToken);
            await UnitOfWork.SaveCapturedSnapshotAsync(captured, cancellationToken);

            lock (_metricsSync)
            {
                _metricsSnapshot = new(
                    _metricsSnapshot.TotalSaves + 1,
                    stopwatch.Elapsed.TotalMilliseconds,
                    start,
                    _metricsSnapshot.SaveErrors
                );
            }
        }
        catch
        {
            lock (_metricsSync)
            {
                _metricsSnapshot = new(
                    _metricsSnapshot.TotalSaves,
                    _metricsSnapshot.LastSaveDurationMs,
                    _metricsSnapshot.LastSaveTimestampUtc,
                    _metricsSnapshot.SaveErrors + 1
                );
            }

            throw;
        }
    }
}
