using Moongate.Persistence.Data.Internal;
using Moongate.Persistence.Interfaces.Persistence;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Serilog;

namespace Moongate.Persistence.Services.Persistence;

internal sealed class HelpTicketRepository : BaseRepository<HelpTicketEntity, Serial>, IHelpTicketRepository
{
    private readonly ILogger _logger = Log.ForContext<HelpTicketRepository>();

    internal HelpTicketRepository(
        PersistenceStateStore stateStore,
        IJournalService journalService,
        IPersistenceEntityDescriptor<HelpTicketEntity, Serial> descriptor
    )
        : base(stateStore, journalService, descriptor) { }

    public ValueTask<IReadOnlyList<HelpTicketEntity>> GetBySenderCharacterIdAsync(
        Serial senderCharacterId,
        CancellationToken cancellationToken = default
    )
    {
        _ = cancellationToken;

        lock (_stateStore.SyncRoot)
        {
            return ValueTask.FromResult<IReadOnlyList<HelpTicketEntity>>(
                [
                    .. _stateStore.HelpTicketsById
                                  .Values
                                  .Where(ticket => ticket.SenderCharacterId == senderCharacterId)
                                  .OrderBy(ticket => ticket.CreatedAtUtc)
                                  .Select(CloneEntity)
                ]
            );
        }
    }

    protected override void BeforeUpsertLocked(HelpTicketEntity entity, HelpTicketEntity? existing)
    {
        _stateStore.LastItemId = Math.Max(_stateStore.LastItemId, (uint)entity.Id);
        _logger.Verbose("Help ticket staged for TicketId={TicketId}", entity.Id);
    }
}
