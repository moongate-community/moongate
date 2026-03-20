using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Persistence.Interfaces.Persistence;

/// <summary>
/// Provides persistence operations for bulletin board message entities.
/// </summary>
public interface IBulletinBoardMessageRepository : IBaseRepository<BulletinBoardMessageEntity, Serial>
{
    /// <summary>
    /// Returns all messages for a board ordered by posting time.
    /// </summary>
    ValueTask<IReadOnlyList<BulletinBoardMessageEntity>> GetByBoardIdAsync(
        Serial boardId,
        CancellationToken cancellationToken = default
    );
}
