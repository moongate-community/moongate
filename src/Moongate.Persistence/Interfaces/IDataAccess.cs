using Moongate.Core.Interfaces.Entities;
using Moongate.Core.Primitives;

namespace Moongate.Persistence.Interfaces;

/// <summary>Provides typed access to one durable entity collection.</summary>
/// <typeparam name="T">The entity type stored in the collection.</typeparam>
public interface IDataAccess<T> where T : class, IMoongateEntity
{
    /// <summary>Gets a detached entity by its stable identity, or null when it is absent.</summary>
    T? GetById(Serial id);

    /// <summary>Gets detached entities from the current committed collection view.</summary>
    IReadOnlyList<T> GetAll();

    /// <summary>Filters a detached committed snapshot of the collection.</summary>
    Task<IReadOnlyList<T>> QueryAsync(Func<T, bool> predicate, CancellationToken cancellationToken = default);

    /// <summary>Durably inserts or replaces an entity by its identity.</summary>
    Task UpsertAsync(T entity, CancellationToken cancellationToken = default);

    /// <summary>Durably deletes an entity by its identity when present.</summary>
    Task<bool> DeleteAsync(Serial id, CancellationToken cancellationToken = default);
}
