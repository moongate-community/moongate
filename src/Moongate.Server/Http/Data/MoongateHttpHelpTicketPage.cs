namespace Moongate.Server.Http.Data;

public sealed class MoongateHttpHelpTicketPage
{
    public required int Page { get; init; }

    public required int PageSize { get; init; }

    public required int TotalCount { get; init; }

    public required IReadOnlyList<MoongateHttpHelpTicket> Items { get; init; }
}
