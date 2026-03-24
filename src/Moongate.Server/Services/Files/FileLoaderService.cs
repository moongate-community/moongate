using System.Diagnostics;
using DryIoc;
using Moongate.Server.Interfaces.Services.Files;
using Serilog;

namespace Moongate.Server.Services.Files;

/// <summary>
/// Represents FileLoaderService.
/// </summary>
public class FileLoaderService : IFileLoaderService
{
    private readonly List<IFileLoader> _fileLoaders = new();

    private readonly ILogger _logger = Log.ForContext<FileLoaderService>();

    private readonly IContainer _container;

    public FileLoaderService(IContainer container)
    {
        _container = container;
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

    public void Dispose()
        => _fileLoaders.Clear();

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
}
