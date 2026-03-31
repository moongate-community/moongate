using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using DryIoc;
using Moongate.Core.Data.Directories;
using Moongate.Server.FileLoaders;
using Moongate.Server.Interfaces.Services.Files;
using Serilog;

namespace Moongate.Server.Services.Files;

/// <summary>
/// Represents FileLoaderService.
/// </summary>
public class FileLoaderService : IFileLoaderService
{
    private readonly List<IFileLoader> _fileLoaders = new();
    private readonly DirectoriesConfig? _directoriesConfig;
    private readonly object _questRenameRecoveryLock = new();
    private readonly Dictionary<string, string> _questRenameRecoveries = new(StringComparer.OrdinalIgnoreCase);

    private readonly ILogger _logger = Log.ForContext<FileLoaderService>();

    private readonly IContainer _container;

    public FileLoaderService(IContainer container, DirectoriesConfig? directoriesConfig = null)
    {
        _container = container;
        _directoriesConfig = directoriesConfig;
    }

    public void AddFileLoader<T>() where T : IFileLoader
        => AddFileLoader(typeof(T));

    public void AddFileLoader(Type loaderType)
    {
        ArgumentNullException.ThrowIfNull(loaderType);

        if (!typeof(IFileLoader).IsAssignableFrom(loaderType))
        {
            throw new InvalidOperationException($"Type '{loaderType.FullName}' does not implement IFileLoader.");
        }

        if (_fileLoaders.Any(loader => loader.GetType() == loaderType))
        {
            return;
        }

        if (!_container.IsRegistered(loaderType))
        {
            _container.Register(loaderType, Reuse.Singleton);
        }

        var fileLoader = (IFileLoader)_container.Resolve(loaderType);
        _fileLoaders.Add(fileLoader);
    }

    public Task LoadSingleAsync(string filePath)
    {
        var loaderType = ResolveLoaderTypeByFilePath(filePath);

        if (loaderType is null)
        {
            throw new InvalidOperationException($"No file loader is registered for path '{filePath}'.");
        }

        return LoadSingleAsync(loaderType, filePath);
    }

    public Task LoadSingleAsync<T>(string filePath) where T : IFileLoader
        => LoadSingleAsync(typeof(T), filePath);

    public Task LoadSingleAsync(Type loaderType, string filePath)
    {
        ArgumentNullException.ThrowIfNull(loaderType);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!typeof(IFileLoader).IsAssignableFrom(loaderType))
        {
            throw new InvalidOperationException($"Type '{loaderType.FullName}' does not implement IFileLoader.");
        }

        if (loaderType == typeof(QuestTemplateLoader))
        {
            return ReloadQuestTemplateAsync(loadedFilePath: filePath);
        }

        var loader = EnsureLoader(loaderType);
        return loader.LoadSingleAsync(filePath);
    }

    public async Task ExecuteLoadersAsync()
    {
        var startTime = Stopwatch.GetTimestamp();

        foreach (var loader in _fileLoaders)
        {
            try
            {
                _logger.Debug("Executing file loader {LoaderType}", loader.GetType().Name);
                await loader.LoadAsync();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error executing file loader {LoaderType}", loader.GetType().Name);

                throw;
            }
        }

        _logger.Information(
            "All file loaders executed in {ElapsedMilliseconds} ms",
            Stopwatch.GetElapsedTime(startTime).Seconds
        );
    }

    public Task StartAsync()
        => ExecuteLoadersAsync();

    public Task StopAsync()
        => Task.CompletedTask;

    public async Task ReloadQuestTemplateAsync(string? removedFilePath = null, string? loadedFilePath = null)
    {
        if (string.IsNullOrWhiteSpace(removedFilePath) && string.IsNullOrWhiteSpace(loadedFilePath))
        {
            return;
        }

        var normalizedRemovedFilePath = NormalizePath(removedFilePath);
        var normalizedLoadedFilePath = NormalizePath(loadedFilePath);

        if (string.IsNullOrWhiteSpace(normalizedRemovedFilePath) && !string.IsNullOrWhiteSpace(normalizedLoadedFilePath))
        {
            if (File.Exists(normalizedLoadedFilePath))
            {
                if (TryConsumeQuestRenameRecovery(normalizedLoadedFilePath, out var recoveredRemovedFilePath))
                {
                    normalizedRemovedFilePath = recoveredRemovedFilePath;
                }
            }
            else if (HasQuestRenameRecovery(normalizedLoadedFilePath))
            {
                return;
            }
        }

        var loader = (QuestTemplateLoader)EnsureLoader(typeof(QuestTemplateLoader));
        var snapshot = loader.CaptureState();
        var validationTarget = normalizedLoadedFilePath ?? normalizedRemovedFilePath;

        try
        {
            if (!string.IsNullOrWhiteSpace(normalizedRemovedFilePath))
            {
                await loader.LoadSingleAsync(normalizedRemovedFilePath);
            }

            if (!string.IsNullOrWhiteSpace(normalizedLoadedFilePath))
            {
                await loader.LoadSingleAsync(normalizedLoadedFilePath);
            }

            if (!string.IsNullOrWhiteSpace(validationTarget))
            {
                await LoadSingleAsync<TemplateValidationLoader>(validationTarget);
            }

            if (!string.IsNullOrWhiteSpace(normalizedLoadedFilePath))
            {
                ClearQuestRenameRecovery(normalizedLoadedFilePath);
            }
        }
        catch
        {
            loader.RestoreState(snapshot);

            if (!string.IsNullOrWhiteSpace(normalizedRemovedFilePath) && !string.IsNullOrWhiteSpace(normalizedLoadedFilePath))
            {
                RememberQuestRenameRecovery(normalizedRemovedFilePath, normalizedLoadedFilePath);
            }

            throw;
        }
    }

    private IFileLoader EnsureLoader(Type loaderType)
    {
        foreach (var loader in _fileLoaders)
        {
            if (loader.GetType() == loaderType)
            {
                return loader;
            }
        }

        AddFileLoader(loaderType);

        foreach (var loader in _fileLoaders)
        {
            if (loader.GetType() == loaderType)
            {
                return loader;
            }
        }

        throw new InvalidOperationException($"Unable to resolve loader '{loaderType.FullName}'.");
    }

    private Type? ResolveLoaderTypeByFilePath(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var extension = Path.GetExtension(filePath);

        if (string.Equals(extension, ".lua", StringComparison.OrdinalIgnoreCase))
        {
            var normalizedLuaPath = NormalizePath(filePath);

            if (IsUnderDirectory(normalizedLuaPath, "scripts", "quests"))
            {
                return typeof(QuestTemplateLoader);
            }

            return null;
        }

        if (!string.Equals(extension, ".json", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var normalizedPath = NormalizePath(filePath);

        if (IsUnderDirectory(normalizedPath, "templates", "items"))
        {
            return typeof(ItemTemplateLoader);
        }

        if (IsUnderDirectory(normalizedPath, "templates", "mobiles"))
        {
            return typeof(MobileTemplateLoader);
        }

        if (IsUnderDirectory(normalizedPath, "templates", "loot"))
        {
            return typeof(LootTemplateLoader);
        }

        if (IsUnderDirectory(normalizedPath, "templates", "factions"))
        {
            return typeof(FactionTemplateLoader);
        }

        if (IsUnderDirectory(normalizedPath, "templates", "sell_profiles"))
        {
            return typeof(SellProfileTemplateLoader);
        }

        if (IsUnderDirectory(normalizedPath, "data", "spawns"))
        {
            return typeof(SpawnsDataLoader);
        }

        return null;
    }

    private bool IsUnderDirectory(string normalizedPath, string rootDirectoryName, string childDirectoryName)
    {
        if (_directoriesConfig is not null)
        {
            var templatesDirectory = GetAbsoluteDirectory(_directoriesConfig[rootDirectoryName], childDirectoryName);

            if (normalizedPath.StartsWith(templatesDirectory, StringComparison.OrdinalIgnoreCase)
                && (normalizedPath.Length == templatesDirectory.Length
                    || normalizedPath[templatesDirectory.Length] == '/'))
            {
                return true;
            }
        }

        return HasFolderChain(normalizedPath, rootDirectoryName, childDirectoryName);
    }

    private static string GetAbsoluteDirectory(string rootDirectory, string childDirectoryName)
    {
        var directoryPath = Path.GetFullPath(Path.Combine(rootDirectory, childDirectoryName));
        var normalized = NormalizePath(directoryPath);

        return normalized.EndsWith('/') ? normalized : normalized + '/';
    }

    private static bool HasFolderChain(string normalizedPath, string first, string second)
    {
        var segments = normalizedPath.Split(
            '/',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
        );

        for (var i = 0; i < segments.Length - 1; i++)
        {
            if (string.Equals(segments[i], first, StringComparison.OrdinalIgnoreCase)
                && string.Equals(segments[i + 1], second, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        return Path.GetFullPath(path)
                   .Replace(Path.DirectorySeparatorChar, '/')
                   .Replace(Path.AltDirectorySeparatorChar, '/');
    }

    private void ClearQuestRenameRecovery(string loadedFilePath)
    {
        var normalizedLoadedFilePath = NormalizePath(loadedFilePath);

        if (string.IsNullOrWhiteSpace(normalizedLoadedFilePath))
        {
            return;
        }

        lock (_questRenameRecoveryLock)
        {
            _questRenameRecoveries.Remove(normalizedLoadedFilePath);
        }
    }

    private bool HasQuestRenameRecovery(string loadedFilePath)
    {
        var normalizedLoadedFilePath = NormalizePath(loadedFilePath);

        if (string.IsNullOrWhiteSpace(normalizedLoadedFilePath))
        {
            return false;
        }

        lock (_questRenameRecoveryLock)
        {
            return _questRenameRecoveries.ContainsKey(normalizedLoadedFilePath);
        }
    }

    private bool TryConsumeQuestRenameRecovery(string loadedFilePath, out string removedFilePath)
    {
        var normalizedLoadedFilePath = NormalizePath(loadedFilePath);

        if (string.IsNullOrWhiteSpace(normalizedLoadedFilePath))
        {
            removedFilePath = string.Empty;

            return false;
        }

        lock (_questRenameRecoveryLock)
        {
            return _questRenameRecoveries.Remove(normalizedLoadedFilePath, out removedFilePath);
        }
    }

    private void RememberQuestRenameRecovery(string removedFilePath, string loadedFilePath)
    {
        var normalizedRemovedFilePath = NormalizePath(removedFilePath);
        var normalizedLoadedFilePath = NormalizePath(loadedFilePath);

        if (string.IsNullOrWhiteSpace(normalizedRemovedFilePath) || string.IsNullOrWhiteSpace(normalizedLoadedFilePath))
        {
            return;
        }

        lock (_questRenameRecoveryLock)
        {
            _questRenameRecoveries[normalizedLoadedFilePath] = normalizedRemovedFilePath;
        }
    }

    public void Dispose()
        => _fileLoaders.Clear();
}
