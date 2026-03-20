using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Persistence.Interfaces.Persistence;

/// <summary>
/// Provides persistence operations for help ticket entities.
/// </summary>
public interface IHelpTicketRepository : IBaseRepository<HelpTicketEntity, Serial>
{
    /// <summary>
    /// Returns tickets for a sender character ordered by creation time.
    /// </summary>
    ValueTask<IReadOnlyList<HelpTicketEntity>> GetBySenderCharacterIdAsync(
        Serial senderCharacterId,
        CancellationToken cancellationToken = default
    );
}
