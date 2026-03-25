using System.Collections.Concurrent;
using Moongate.Core.Data.Directories;
using Moongate.Core.Types;
using Moongate.Scripting.Interfaces;
using Moongate.Server.Data.Config;
using Moongate.Server.Interfaces.Services.EvenLoop;
using Moongate.Server.Interfaces.Services.Files;
using Serilog;

namespace Moongate.Server.Services.Files;

/// <summary>
/// Centralized watcher for Lua scripts and reloadable JSON runtime files.
/// </summary>
public sealed class FileWatcherService : IFileWatcherService
{
    private static readonly TimeSpan DebounceDelay = TimeSpan.FromMilliseconds(300);

    private readonly IBackgroundJobService _backgroundJobService;
    private readonly ConcurrentDictionary<string, Timer> _debounceTimers = new(StringComparer.OrdinalIgnoreCase);
    private readonly DirectoriesConfig _directoriesConfig;
    private readonly IFileLoaderService _fileLoaderService;
    private readonly ILogger _logger = Log.ForContext<FileWatcherService>();
    private readonly MoongateConfig _moongateConfig;
    private readonly IScriptEngineService _scriptEngineService;
    private readonly List<FileSystemWatcher> _watchers = [];

    public FileWatcherService(
        DirectoriesConfig directoriesConfig,
        MoongateConfig moongateConfig,
        IBackgroundJobService backgroundJobService,
        IScriptEngineService scriptEngineService,
        IFileLoaderService fileLoaderService
    )
    {
        _directoriesConfig = directoriesConfig;
        _moongateConfig = moongateConfig;
        _backgroundJobService = backgroundJobService;
        _scriptEngineService = scriptEngineService;
        _fileLoaderService = fileLoaderService;
    }

    public Task StartAsync()
    {
        if (!_moongateConfig.Scripting.EnableFileWatcher)
        {
            _logger.Information("Runtime file watcher disabled by configuration.");

            return Task.CompletedTask;
        }

        RegisterWatcher(_directoriesConfig[DirectoryType.Scripts], "*.lua");
        RegisterWatcher(_directoriesConfig[DirectoryType.Templates], "*.json");
        RegisterWatcher(Path.Combine(_directoriesConfig[DirectoryType.Data], "spawns"), "*.json");

        _logger.Information("Runtime file watcher started. Watchers={WatcherCount}", _watchers.Count);

        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        foreach (var watcher in _watchers)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
        }

        _watchers.Clear();

        foreach (var (_, timer) in _debounceTimers)
        {
            timer.Dispose();
        }

        _debounceTimers.Clear();

        _logger.Information("Runtime file watcher stopped.");

        return Task.CompletedTask;
    }

    private void RegisterWatcher(string path, string filter)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }

        var watcher = new FileSystemWatcher(path, filter)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime | NotifyFilters.Size,
            IncludeSubdirectories = true,
            EnableRaisingEvents = true
        };

        watcher.Changed += OnFileChanged;
        watcher.Created += OnFileChanged;
        watcher.Renamed += OnFileRenamed;
        _watchers.Add(watcher);
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
        => ScheduleReload(e.FullPath);

    private void OnFileRenamed(object sender, RenamedEventArgs e)
        => ScheduleReload(e.FullPath);

    private void ScheduleReload(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        var normalizedPath = Path.GetFullPath(filePath);
        var timer = _debounceTimers.AddOrUpdate(
            normalizedPath,
            path => new Timer(OnDebounceTimerElapsed, path, DebounceDelay, Timeout.InfiniteTimeSpan),
            (_, existingTimer) =>
            {
                existingTimer.Change(DebounceDelay, Timeout.InfiniteTimeSpan);

                return existingTimer;
            }
        );

        timer.Change(DebounceDelay, Timeout.InfiniteTimeSpan);
    }

    private void OnDebounceTimerElapsed(object? state)
    {
        if (state is not string filePath)
        {
            return;
        }

        if (_debounceTimers.TryRemove(filePath, out var timer))
        {
            timer.Dispose();
        }

        _backgroundJobService.PostToGameLoop(() => ProcessChangeOnGameLoop(filePath));
    }

    private void ProcessChangeOnGameLoop(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                _logger.Debug("Skipping hot reload for deleted file {FilePath}", filePath);

                return;
            }

            if (string.Equals(Path.GetExtension(filePath), ".lua", StringComparison.OrdinalIgnoreCase))
            {
                _scriptEngineService.InvalidateScript(filePath);
                _logger.Information("[HotReload] Reloaded script: {FilePath}", filePath);

                return;
            }

            if (string.Equals(Path.GetExtension(filePath), ".json", StringComparison.OrdinalIgnoreCase))
            {
                _fileLoaderService.LoadSingleAsync(filePath).GetAwaiter().GetResult();
                _logger.Information("[HotReload] Reloaded data file: {FilePath}", filePath);
            }
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Hot reload failed for {FilePath}", filePath);
        }
    }
}
