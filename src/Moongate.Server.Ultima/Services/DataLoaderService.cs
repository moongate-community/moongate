using DryIoc;
using Moongate.Server.Ultima.Data.Internal;
using Moongate.Server.Ultima.Interfaces.Loaders;

namespace Moongate.Server.Ultima.Services;

/// <inheritdoc cref="IDataLoaderService"/>
public sealed class DataLoaderService : IDataLoaderService
{
    private readonly IResolverContext _resolver;
    private readonly Dictionary<Type, object> _entitiesByType = new();

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
            _entitiesByType[registration.EntityType] = await registration.RunAsync(_resolver, CancellationToken.None);
        }
    }

    /// <inheritdoc />
    public Task StopAsync()
        => Task.CompletedTask;

    /// <inheritdoc />
    public IReadOnlyList<TEntity> Get<TEntity>()
    {
        if (!_entitiesByType.TryGetValue(typeof(TEntity), out var entities))
        {
            throw new InvalidOperationException($"No data loader is registered for {typeof(TEntity).Name}.");
        }

        return (IReadOnlyList<TEntity>)entities;
    }
}
