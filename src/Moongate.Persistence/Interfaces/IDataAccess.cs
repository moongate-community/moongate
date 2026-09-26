using System.Linq.Expressions;
using Moongate.Core.Interfaces.Entities;
using Moongate.Core.Primitives;

namespace Moongate.Persistence.Interfaces;

/// <summary>
///     Provides asynchronous PostgreSQL access to detached entities.
/// </summary>
/// <typeparam name="T">
///     The registered entity type.
/// </typeparam>
public interface IDataAccess<T> where T : class, IMoongateEntity
{
    /// <summary>
    ///     Deletes a nonzero identity and reports whether a row existed.
    /// </summary>
    Task<bool> DeleteAsync(Serial id, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Reads all detached entities. This is an intentionally unbounded startup/admin operation.
    /// </summary>
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Reads a detached entity, or null if its nonzero identity is absent.
    /// </summary>
    Task<T?> GetByIdAsync(Serial id, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Filters entities in SQL; unsupported expressions fail instead of filtering on the client.
    /// </summary>
    Task<IReadOnlyList<T>> QueryAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Filters in SQL and returns an identity-ordered page; skip must be nonnegative and take positive.
    /// </summary>
    Task<IReadOnlyList<T>> QueryAsync(
        Expression<Func<T, bool>> predicate,
        int skip,
        int take,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    ///     Assigns an identity and inserts an entity with a zero ID, or upserts an explicitly identified entity.
    /// </summary>
    /// <remarks>
    ///     Automatic assignment requires a public Id setter and a migration-managed, column-owned sequence.
    ///     The assigned ID is written back to the entity. A failed insert restores zero; a later transaction rollback
    ///     retains the assigned ID, and sequence reservations are never reclaimed. Nonzero IDs use last-writer-wins semantics.
    ///     The caller must not mutate the input during execution. No version conflict detection is implied.
    /// </remarks>
    Task UpsertAsync(T entity, CancellationToken cancellationToken = default);
}
