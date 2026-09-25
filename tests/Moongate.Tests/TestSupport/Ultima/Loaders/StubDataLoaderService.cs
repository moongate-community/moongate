using Moongate.Server.Ultima.Interfaces.Loaders;

namespace Moongate.Tests.TestSupport.Ultima.Loaders;

/// <summary>
///     An <see cref="IDataLoaderService" /> that serves entities registered in advance instead of running loaders.
///     A type with nothing registered returns an empty list.
/// </summary>
public sealed class StubDataLoaderService : IDataLoaderService
{
    private readonly Dictionary<Type, object> _entitiesByType = new();

    public StubDataLoaderService With<TEntity>(params TEntity[] entities)
    {
        _entitiesByType[typeof(TEntity)] = entities;

        return this;
    }

    public Task StartAsync()
    {
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        return Task.CompletedTask;
    }

    public IReadOnlyList<TEntity> GetEntities<TEntity>()
    {
        return _entitiesByType.TryGetValue(typeof(TEntity), out var entities)
            ? (IReadOnlyList<TEntity>)entities
            : [];
    }
}
