using System.Diagnostics;
using DryIoc;
using Moongate.Server.Ultima.Data.Internal;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Serilog;

namespace Moongate.Server.Ultima.Services;

/// <inheritdoc cref="IDataLoaderService" />
public sealed class DataLoaderService : IDataLoaderService
{
    private readonly IResolverContext _resolver;
    private readonly Dictionary<Type, object> _entitiesByType = new();

    private readonly ILogger _logger = Log.ForContext<DataLoaderService>();

    public DataLoaderService(IResolverContext resolver)
    {
        _resolver = resolver;
    }

    /// <inheritdoc />
    public async Task StartAsync()
    {
        var registrations = _resolver.Resolve<List<DataLoaderRegistration>>(IfUnresolved.ReturnDefault) ?? [];

        foreach (var registration in registrations.OrderBy(registration => registration.Priority))
        {
            _logger.Information(
                "Running data loader for {EntityType} using {LoaderType}.",
                registration.EntityType.Name,
                registration.LoaderType.Name
            );
            var startTime = Stopwatch.GetTimestamp();
            _entitiesByType[registration.EntityType] = await registration.RunAsync(_resolver, CancellationToken.None);
            _logger.Information(
                "Data loader for {EntityType} completed in {ElapsedMilliseconds} ms.",
                registration.EntityType.Name,
                Stopwatch.GetElapsedTime(startTime)
            );
        }
    }

    /// <inheritdoc />
    public Task StopAsync()
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public IReadOnlyList<TEntity> GetEntities<TEntity>()
    {
        if (!_entitiesByType.TryGetValue(typeof(TEntity), out var entities))
        {
            throw new InvalidOperationException($"No data loader is registered for {typeof(TEntity).Name}.");
        }

        return (IReadOnlyList<TEntity>)entities;
    }
}
