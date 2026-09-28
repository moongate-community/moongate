using Moongate.Persistence.Interfaces;
using Moongate.Persistence.Services;
using Moongate.Persistence.Types.Persistence;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Runs world-database transactions through the persistence owner, which serializes them with the world saves.
/// </summary>
public sealed class WorldTransactionService : IWorldTransactionService
{
    private readonly MoongatePersistenceService _persistence;

    public WorldTransactionService(MoongatePersistenceService persistence)
    {
        _persistence = persistence;
    }

    public Task ExecuteAsync(Func<IPersistenceTransaction, Task> operation, CancellationToken cancellationToken = default)
    {
        return _persistence.ExecuteInTransactionAsync(PersistenceDatabaseTarget.Realm, operation, cancellationToken);
    }
}
