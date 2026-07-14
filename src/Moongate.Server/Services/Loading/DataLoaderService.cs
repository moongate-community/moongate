using Moongate.Server.Interfaces.Loading;
using Serilog;
using SquidStd.Abstractions.Interfaces.Services;

namespace Moongate.Server.Services.Loading;

/// <summary>
/// Executes the registered <see cref="IDataLoader" />s once at startup. The loaders are handed in
/// already ordered by priority (see the RegisterDataLoader registration); this service just runs them.
/// </summary>
public sealed class DataLoaderService : IDataLoaderService, ISquidStdService
{
    private readonly ILogger _logger = Log.ForContext<DataLoaderService>();
    private readonly IReadOnlyList<IDataLoader> _loaders;

    public DataLoaderService(IReadOnlyList<IDataLoader> loaders)
    {
        _loaders = loaders;
    }

    public async ValueTask ExecuteLoadersAsync(CancellationToken ct = default)
    {
        foreach (var loader in _loaders)
        {
            _logger.Debug("Executing loader {loader}", loader.GetType().Name);
            await loader.LoadAsync(ct);
        }

        _logger.Information("Executed {Count} data loader(s)", _loaders.Count);
    }

    public async ValueTask StartAsync(CancellationToken cancellationToken = default)
        => await ExecuteLoadersAsync(cancellationToken);

    public ValueTask StopAsync(CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;
}
