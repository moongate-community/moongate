using Moongate.Abstractions.Interfaces.Services.Base;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Interfaces.Services.Interaction;

public interface IHelpTicketService : IMoongateService
{
    Task<HelpTicketEntity?> AssignToAccountAsync(
        Serial ticketId,
        Serial assignedToAccountId,
        Serial? assignedToCharacterId,
        CancellationToken cancellationToken = default
    );

    Task<HelpTicketEntity?> CreateTicketAsync(
        long sessionId,
        HelpTicketCategory category,
        string message,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<HelpTicketEntity>> GetAllTicketsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<HelpTicketEntity>> GetOpenTicketsForAccountAsync(
        Serial senderAccountId,
        CancellationToken cancellationToken = default
    );

    Task<HelpTicketEntity?> GetTicketByIdAsync(Serial ticketId, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<HelpTicketEntity> Items, int TotalCount)> GetTicketsForAdminAsync(
        int page,
        int pageSize,
        HelpTicketStatus? status,
        HelpTicketCategory? category,
        Serial? assignedToAccountId,
        CancellationToken cancellationToken = default
    );

    Task<HelpTicketEntity?> UpdateStatusAsync(
        Serial ticketId,
        HelpTicketStatus status,
        CancellationToken cancellationToken = default
    );
}
