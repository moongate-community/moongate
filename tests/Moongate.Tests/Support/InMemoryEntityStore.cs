using Moongate.Core.Primitives;
using Moongate.Persistence.Interfaces;
using SquidStd.Persistence.Abstractions.Data;
using SquidStd.Persistence.Abstractions.Interfaces.Persistence;

namespace Moongate.Tests.Support;

/// <summary>
/// In-memory <see cref="IEntityStore{TEntity,Serial}" /> test double. Mirrors the real store's auto-id
/// behaviour: an upsert of an entity with a zero serial allocates the next serial and writes it back,
/// starting from the entity kind's own range (items live above <see cref="Serial.MinItem" />) so that
/// <see cref="Serial.IsItem" /> / <see cref="Serial.IsMobile" /> hold for test entities too.
/// </summary>
public sealed class InMemoryEntityStore<TEntity> : IEntityStore<TEntity, Serial>
    where TEntity : class, ISerialIdEntity
{
    private readonly Dictionary<Serial, TEntity> _items = new();
    private uint _nextId = typeof(TEntity) == typeof(Moongate.Persistence.Entities.ItemEntity) ? Serial.MinItem : 1;

    public int Count()
        => _items.Count;

    public ValueTask<int> CountAsync(CancellationToken cancellationToken = default)
        => new(Count());

    public IReadOnlyCollection<TEntity> GetAll()
        => _items.Values.ToList();

    public ValueTask<IReadOnlyCollection<TEntity>> GetAllAsync(CancellationToken cancellationToken = default)
        => new(GetAll());

    public TEntity? GetById(Serial id)
        => _items.TryGetValue(id, out var entity) ? entity : null;

    public ValueTask<TEntity?> GetByIdAsync(Serial id, CancellationToken cancellationToken = default)
        => new(GetById(id));

    public IQueryable<TEntity> Query()
        => _items.Values.AsQueryable();

    /// <summary>
    /// Mirrors the real store's ordering, ties included: equal sort keys break on the entity's serial, so
    /// a test cannot pass against a page order production would not produce. Unlike the real store this
    /// hands back live references rather than clones — the simplification the rest of this double already
    /// makes.
    /// </summary>
    public PagedResult<TEntity> QueryPaged<TOrder>(
        Func<TEntity, bool>? filter,
        Func<TEntity, TOrder> orderBy,
        int skip,
        int take,
        bool descending = false
    )
    {
        var matched = filter is null ? _items.Values.ToList() : _items.Values.Where(filter).ToList();

        var ordered = descending
                          ? matched.OrderByDescending(orderBy).ThenByDescending(entity => entity.Id)
                          : matched.OrderBy(orderBy).ThenBy(entity => entity.Id);

        IReadOnlyList<TEntity> page = [.. ordered.Skip(skip).Take(take)];

        return new(page, matched.Count, skip, take);
    }

    public ValueTask<bool> RemoveAsync(Serial id, CancellationToken cancellationToken = default)
        => new(_items.Remove(id));

    public ValueTask UpsertAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        if (entity.Id == Serial.Zero)
        {
            entity.Id = (Serial)_nextId++;
        }

        _items[entity.Id] = entity;

        return ValueTask.CompletedTask;
    }
}
