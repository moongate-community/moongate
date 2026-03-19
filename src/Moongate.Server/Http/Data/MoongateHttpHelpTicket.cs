namespace Moongate.Server.Http.Data;

public sealed class MoongateHttpHelpTicket
{
    public string TicketId { get; set; } = string.Empty;

    public string SenderCharacterId { get; set; } = string.Empty;

    public string SenderAccountId { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public int MapId { get; set; }

    public int X { get; set; }

    public int Y { get; set; }

    public int Z { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? AssignedAtUtc { get; set; }

    public DateTime? ClosedAtUtc { get; set; }

    public DateTime LastUpdatedAtUtc { get; set; }

    public string AssignedToCharacterId { get; set; } = string.Empty;

    public string AssignedToAccountId { get; set; } = string.Empty;
}
