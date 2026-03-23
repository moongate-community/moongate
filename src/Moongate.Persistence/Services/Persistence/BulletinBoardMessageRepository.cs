using Moongate.Persistence.Data.Internal;
using Moongate.Persistence.Interfaces.Persistence;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Serilog;

namespace Moongate.Persistence.Services.Persistence;

internal sealed class BulletinBoardMessageRepository
    : BaseRepository<BulletinBoardMessageEntity, Serial>,
      IBulletinBoardMessageRepository
{
    private readonly ILogger _logger = Log.ForContext<BulletinBoardMessageRepository>();

    internal BulletinBoardMessageRepository(
        PersistenceStateStore stateStore,
        IJournalService journalService,
        IPersistenceEntityDescriptor<BulletinBoardMessageEntity, Serial> descriptor
    )
        : base(stateStore, journalService, descriptor) { }

    public ValueTask<IReadOnlyList<BulletinBoardMessageEntity>> GetByBoardIdAsync(
        Serial boardId,
        CancellationToken cancellationToken = default
    )
    {
        _ = cancellationToken;

        lock (_stateStore.SyncRoot)
        {
            return ValueTask.FromResult<IReadOnlyList<BulletinBoardMessageEntity>>(
                [
                    .. _stateStore.BulletinBoardMessagesById
                                  .Values
                                  .Where(message => message.BoardId == boardId)
                                  .OrderBy(message => message.PostedAtUtc)
                                  .Select(CloneEntity)
                ]
            );
        }
    }

    protected override void BeforeUpsertLocked(BulletinBoardMessageEntity entity, BulletinBoardMessageEntity? existing)
    {
        _stateStore.LastItemId = Math.Max(_stateStore.LastItemId, (uint)entity.MessageId);
        _logger.Verbose("Bulletin board message staged for MessageId={MessageId}", entity.MessageId);
    }
}
