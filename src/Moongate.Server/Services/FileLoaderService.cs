using DryIoc;
using Moongate.UO.Interfaces.FileLoaders;
using Moongate.UO.Interfaces.Services;
using Serilog;

namespace Moongate.Server.Services;

public class FileLoaderService : IFileLoaderService
{
    private readonly List<IFileLoader> _fileLoaders = new();

    private readonly ILogger _logger = Log.ForContext<FileLoaderService>();

    private readonly IContainer _container;

    public FileLoaderService(IContainer container)
    {
        _container = container;
    }

    public void Dispose()
    {
        _fileLoaders.Clear();
    }

    public void AddFileLoader<T>() where T : IFileLoader
    {
        if (!_container.IsRegistered<T>())
        {
            _container.Register<T>();
        }

        var fileLoader = _container.Resolve<T>();
        if (!_fileLoaders.Contains(fileLoader))
        {
            _fileLoaders.Add(fileLoader);
        }
    }

    public async Task ExecuteLoadersAsync()
    {
        foreach (var loader in _fileLoaders)
        {
            try
            {
                _logger.Information("Executing file loader {LoaderType}", loader.GetType().Name);
                await loader.LoadAsync();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error executing file loader {LoaderType}", loader.GetType().Name);
                throw;
            }
        }
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        return ExecuteLoadersAsync();
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
