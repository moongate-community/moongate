namespace Moongate.Persistence.Interfaces.Persistence;

/// <summary>
/// Common persistence operations shared by registered entity repositories.
/// </summary>
public interface IBaseRepository<TEntity, in TKey>
{
    /// <summary>
    /// Returns the current number of persisted entities.
    /// </summary>
    ValueTask<int> CountAsync(CancellationToken cancellationToken = default)
        => ValueTask.FromException<int>(new NotSupportedException());

    /// <summary>
    /// Returns all persisted entities.
    /// </summary>
    ValueTask<IReadOnlyCollection<TEntity>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an entity by its identifier.
    /// </summary>
    ValueTask<TEntity?> GetByIdAsync(TKey id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes an entity by its identifier.
    /// </summary>
    ValueTask<bool> RemoveAsync(TKey id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts or updates an entity.
    /// </summary>
    ValueTask UpsertAsync(TEntity entity, CancellationToken cancellationToken = default);
}
