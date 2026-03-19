using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Persistence.Interfaces.Persistence;

public interface IHelpTicketRepository
{
    ValueTask<IReadOnlyCollection<HelpTicketEntity>> GetAllAsync(CancellationToken cancellationToken = default);

    ValueTask<HelpTicketEntity?> GetByIdAsync(Serial ticketId, CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<HelpTicketEntity>> GetBySenderCharacterIdAsync(
        Serial senderCharacterId,
        CancellationToken cancellationToken = default
    );

    ValueTask<bool> RemoveAsync(Serial ticketId, CancellationToken cancellationToken = default);

    ValueTask UpsertAsync(HelpTicketEntity ticket, CancellationToken cancellationToken = default);
}
