using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Persistence.Interfaces.Persistence;

public interface IBulletinBoardMessageRepository
{
    ValueTask<IReadOnlyCollection<BulletinBoardMessageEntity>> GetAllAsync(CancellationToken cancellationToken = default);

    ValueTask<BulletinBoardMessageEntity?> GetByIdAsync(Serial messageId, CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<BulletinBoardMessageEntity>> GetByBoardIdAsync(Serial boardId, CancellationToken cancellationToken = default);

    ValueTask UpsertAsync(BulletinBoardMessageEntity message, CancellationToken cancellationToken = default);

    ValueTask<bool> RemoveAsync(Serial messageId, CancellationToken cancellationToken = default);
}
