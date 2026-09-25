using Moongate.Core.Interfaces.Entities;
using Moongate.Core.Primitives;

namespace Moongate.Persistence.Interfaces;

/// <summary>
///     Provides sequential access within one database transaction callback.
/// </summary>
/// <remarks>
///     Facades expire when the callback ends. Failed operations poison the transaction even when caught.
/// </remarks>
public interface IPersistenceTransaction
{
    /// <summary>
    ///     Reads an entity and holds a PostgreSQL row lock until this transaction finishes.
    /// </summary>
    /// <remarks>
    ///     Rejects zero IDs, other database targets and calls outside the callback scope.
    /// </remarks>
    Task<T?> GetByIdForUpdateAsync<T>(Serial id, CancellationToken cancellationToken = default)
        where T : class, IMoongateEntity;

    /// <summary>
    ///     Gets a facade owned by this transaction's database target.
    /// </summary>
    IDataAccess<T> GetDataAccess<T>() where T : class, IMoongateEntity;
}
