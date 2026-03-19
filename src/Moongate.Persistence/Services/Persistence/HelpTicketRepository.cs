using Moongate.Persistence.Data.Internal;
using Moongate.Persistence.Data.Persistence;
using Moongate.Persistence.Interfaces.Persistence;
using Moongate.Persistence.Types;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Serilog;

namespace Moongate.Persistence.Services.Persistence;

public sealed class HelpTicketRepository : IHelpTicketRepository
{
    private readonly IJournalService _journalService;
    private readonly ILogger _logger = Log.ForContext<HelpTicketRepository>();
    private readonly PersistenceStateStore _stateStore;

    internal HelpTicketRepository(PersistenceStateStore stateStore, IJournalService journalService)
    {
        _stateStore = stateStore;
        _journalService = journalService;
    }

    public ValueTask<IReadOnlyCollection<HelpTicketEntity>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;

        lock (_stateStore.SyncRoot)
        {
            return ValueTask.FromResult<IReadOnlyCollection<HelpTicketEntity>>(
                [.. _stateStore.HelpTicketsById.Values.Select(Clone)]
            );
        }
    }

    public ValueTask<HelpTicketEntity?> GetByIdAsync(Serial ticketId, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;

        lock (_stateStore.SyncRoot)
        {
            return ValueTask.FromResult(
                _stateStore.HelpTicketsById.TryGetValue(ticketId, out var ticket)
                    ? Clone(ticket)
                    : null
            );
        }
    }

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
                                  .Select(Clone)
                ]
            );
        }
    }

    public async ValueTask<bool> RemoveAsync(Serial ticketId, CancellationToken cancellationToken = default)
    {
        var removed = false;
        JournalEntry? entry = null;

        lock (_stateStore.SyncRoot)
        {
            if (_stateStore.HelpTicketsById.Remove(ticketId))
            {
                removed = true;
                entry = CreateEntry(PersistenceOperationType.RemoveHelpTicket, JournalPayloadCodec.EncodeSerial(ticketId));
            }
        }

        if (removed && entry is not null)
        {
            await _journalService.AppendAsync(entry, cancellationToken);
        }

        return removed;
    }

    public async ValueTask UpsertAsync(HelpTicketEntity ticket, CancellationToken cancellationToken = default)
    {
        JournalEntry entry;

        lock (_stateStore.SyncRoot)
        {
            var clone = Clone(ticket);
            _stateStore.HelpTicketsById[clone.Id] = clone;
            _stateStore.LastItemId = Math.Max(_stateStore.LastItemId, (uint)clone.Id);
            entry = CreateEntry(PersistenceOperationType.UpsertHelpTicket, JournalPayloadCodec.EncodeHelpTicket(clone));
        }

        await _journalService.AppendAsync(entry, cancellationToken);
        _logger.Verbose("Help ticket upsert completed for TicketId={TicketId}", ticket.Id);
    }

    private static HelpTicketEntity Clone(HelpTicketEntity ticket)
        => SnapshotMapper.ToHelpTicketEntity(SnapshotMapper.ToHelpTicketSnapshot(ticket));

    private JournalEntry CreateEntry(PersistenceOperationType operationType, byte[] payload)
        => new()
        {
            SequenceId = ++_stateStore.LastSequenceId,
            TimestampUnixMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            OperationType = operationType,
            Payload = payload
        };
}
