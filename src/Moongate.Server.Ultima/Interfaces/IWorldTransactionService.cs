using Moongate.Persistence.Interfaces;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     Runs several world-database writes as one transaction: all of them commit, or none.
/// </summary>
public interface IWorldTransactionService
{
    /// <summary>
    ///     Runs <paramref name="operation" /> in one transaction of the world database.
    /// </summary>
    Task ExecuteAsync(Func<IPersistenceTransaction, Task> operation, CancellationToken cancellationToken = default);
}
