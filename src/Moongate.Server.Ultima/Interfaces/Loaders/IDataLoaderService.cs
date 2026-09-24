using Moongate.Server.Core.Interfaces.Services;

namespace Moongate.Server.Ultima.Interfaces.Loaders;

/// <summary>
/// Runs every registered <see cref="IDataLoader{TEntity}" /> at startup and keeps each loader's
/// entities available by type.
/// </summary>
public interface IDataLoaderService : IMoongateStartupService
{
    /// <summary>Gets the entities the loader registered for <typeparamref name="TEntity" /> produced.</summary>
    /// <exception cref="InvalidOperationException">No loader is registered for <typeparamref name="TEntity" />.</exception>
    IReadOnlyList<TEntity> GetEntities<TEntity>();
}
