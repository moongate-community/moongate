using Moongate.Persistence.Interfaces;
using SquidStd.Persistence.Abstractions.Interfaces.Persistence;

namespace Moongate.Tests.Support;

/// <summary>
/// Test double for <see cref="IPersistenceService" /> that hands out per-type
/// <see cref="InMemoryEntityStore{TEntity}" /> instances. <see cref="GetStore{TEntity,TKey}" /> and the
/// typed <see cref="Store{TEntity}" /> helper share one cache, so the service under test and the test's
/// arrange code observe the same store instance.
/// </summary>
public sealed class FakePersistenceService : IPersistenceService
{
    private readonly Dictionary<Type, object> _stores = new();

    public IEntityStore<TEntity, TKey> GetStore<TEntity, TKey>()
        where TKey : notnull
    {
        if (!_stores.TryGetValue(typeof(TEntity), out var store))
        {
            store = Activator.CreateInstance(typeof(InMemoryEntityStore<>).MakeGenericType(typeof(TEntity)))!;
            _stores[typeof(TEntity)] = store;
        }

        return (IEntityStore<TEntity, TKey>)store;
    }

    public ValueTask InitializeAsync(CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    public ValueTask SaveSnapshotAsync(CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    public ValueTask StartAsync(CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    public ValueTask StopAsync(CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    /// <summary>Returns the in-memory store for <typeparamref name="TEntity" />, creating it on first use.</summary>
    public InMemoryEntityStore<TEntity> Store<TEntity>()
        where TEntity : class, ISerialIdEntity
    {
        if (!_stores.TryGetValue(typeof(TEntity), out var store))
        {
            store = new InMemoryEntityStore<TEntity>();
            _stores[typeof(TEntity)] = store;
        }

        return (InMemoryEntityStore<TEntity>)store;
    }
}
