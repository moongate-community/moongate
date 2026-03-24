using Moongate.Server.Data.Events.Help;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Interaction;
using Moongate.Server.Interfaces.Services.Persistence;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Services.Interaction;

public sealed class HelpTicketService : IHelpTicketService
{
    private readonly IGameNetworkSessionService _gameNetworkSessionService;
    private readonly IPersistenceService _persistenceService;
    private readonly IGameEventBusService _gameEventBusService;

    public HelpTicketService(
        IGameNetworkSessionService gameNetworkSessionService,
        IPersistenceService persistenceService,
        IGameEventBusService gameEventBusService
    )
    {
        _gameNetworkSessionService = gameNetworkSessionService;
        _persistenceService = persistenceService;
        _gameEventBusService = gameEventBusService;
    }

    public async Task<HelpTicketEntity?> AssignToAccountAsync(
        Serial ticketId,
        Serial assignedToAccountId,
        Serial? assignedToCharacterId,
        CancellationToken cancellationToken = default
    )
    {
        var ticket = await _persistenceService.UnitOfWork.HelpTickets.GetByIdAsync(ticketId, cancellationToken);

        if (ticket is null)
        {
            return null;
        }

        var now = DateTime.UtcNow;
        ticket.Status = HelpTicketStatus.Assigned;
        ticket.AssignedToAccountId = assignedToAccountId;
        ticket.AssignedToCharacterId = assignedToCharacterId ?? Serial.Zero;
        ticket.AssignedAtUtc = now;
        ticket.LastUpdatedAtUtc = now;

        await _persistenceService.UnitOfWork.HelpTickets.UpsertAsync(ticket, cancellationToken);

        return ticket;
    }

    public async Task<HelpTicketEntity?> CreateTicketAsync(
        long sessionId,
        HelpTicketCategory category,
        string message,
        CancellationToken cancellationToken = default
    )
    {
        if (sessionId <= 0 || string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        if (!_gameNetworkSessionService.TryGet(sessionId, out var session) ||
            session.CharacterId == 0 ||
            session.AccountId == 0 ||
            session.Character is null)
        {
            return null;
        }

        var now = DateTime.UtcNow;
        var trimmedMessage = message.Trim();
        var ticket = new HelpTicketEntity
        {
            Id = _persistenceService.UnitOfWork.AllocateNextItemId(),
            SenderCharacterId = session.CharacterId,
            SenderAccountId = session.AccountId,
            Category = category,
            Message = trimmedMessage,
            MapId = session.Character.MapId,
            Location = session.Character.Location,
            Status = HelpTicketStatus.Open,
            CreatedAtUtc = now,
            LastUpdatedAtUtc = now
        };

        await _persistenceService.UnitOfWork.HelpTickets.UpsertAsync(ticket, cancellationToken);
        await _gameEventBusService.PublishAsync(
            new TicketOpenedEvent(
                ticket.Id,
                ticket.SenderCharacterId,
                ticket.SenderAccountId,
                ticket.Category,
                ticket.Message,
                ticket.MapId,
                ticket.Location
            ),
            cancellationToken
        );

        return ticket;
    }

    public async Task<IReadOnlyList<HelpTicketEntity>> GetAllTicketsAsync(CancellationToken cancellationToken = default)
        =>
        [
            .. (await _persistenceService.UnitOfWork.HelpTickets.GetAllAsync(cancellationToken)).OrderBy(
                ticket => ticket.CreatedAtUtc
            )
        ];

    public async Task<IReadOnlyList<HelpTicketEntity>> GetOpenTicketsForAccountAsync(
        Serial senderAccountId,
        CancellationToken cancellationToken = default
    )
        =>
        [
            .. (await _persistenceService.UnitOfWork.HelpTickets.GetAllAsync(cancellationToken)).Where(
                ticket => ticket.SenderAccountId == senderAccountId &&
                          ticket.Status is HelpTicketStatus.Open or HelpTicketStatus.Assigned
            )
            .OrderBy(ticket => ticket.CreatedAtUtc)
        ];

    public async Task<HelpTicketEntity?> GetTicketByIdAsync(Serial ticketId, CancellationToken cancellationToken = default)
        => await _persistenceService.UnitOfWork.HelpTickets.GetByIdAsync(ticketId, cancellationToken);

    public async Task<(IReadOnlyList<HelpTicketEntity> Items, int TotalCount)> GetTicketsForAdminAsync(
        int page,
        int pageSize,
        HelpTicketStatus? status,
        HelpTicketCategory? category,
        Serial? assignedToAccountId,
        CancellationToken cancellationToken = default
    )
    {
        var safePage = Math.Max(page, 1);
        var safePageSize = Math.Clamp(pageSize <= 0 ? 50 : pageSize, 1, 200);
        IEnumerable<HelpTicketEntity> tickets =
            await _persistenceService.UnitOfWork.HelpTickets.GetAllAsync(cancellationToken);

        if (status is not null)
        {
            tickets = tickets.Where(ticket => ticket.Status == status.Value);
        }

        if (category is not null)
        {
            tickets = tickets.Where(ticket => ticket.Category == category.Value);
        }

        if (assignedToAccountId is not null)
        {
            tickets = tickets.Where(ticket => ticket.AssignedToAccountId == assignedToAccountId.Value);
        }

        var filtered = tickets.OrderByDescending(ticket => ticket.CreatedAtUtc).ToList();
        var items = filtered.Skip((safePage - 1) * safePageSize)
                            .Take(safePageSize)
                            .ToList();

        return (items, filtered.Count);
    }

    public Task StartAsync()
        => Task.CompletedTask;

    public Task StopAsync()
        => Task.CompletedTask;

    public async Task<HelpTicketEntity?> UpdateStatusAsync(
        Serial ticketId,
        HelpTicketStatus status,
        CancellationToken cancellationToken = default
    )
    {
        var ticket = await _persistenceService.UnitOfWork.HelpTickets.GetByIdAsync(ticketId, cancellationToken);

        if (ticket is null)
        {
            return null;
        }

        ticket.Status = status;
        ticket.LastUpdatedAtUtc = DateTime.UtcNow;

        if (status == HelpTicketStatus.Closed)
        {
            ticket.ClosedAtUtc = ticket.LastUpdatedAtUtc;
        }

        await _persistenceService.UnitOfWork.HelpTickets.UpsertAsync(ticket, cancellationToken);

        return ticket;
    }
}
