using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Types;

namespace Moongate.UO.Data.Persistence.Entities;

public sealed class HelpTicketEntity
{
    public Serial Id { get; set; }

    public Serial SenderCharacterId { get; set; }

    public Serial SenderAccountId { get; set; }

    public HelpTicketCategory Category { get; set; }

    public string Message { get; set; } = string.Empty;

    public int MapId { get; set; }

    public Point3D Location { get; set; }

    public HelpTicketStatus Status { get; set; }

    public Serial AssignedToCharacterId { get; set; }

    public Serial AssignedToAccountId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? AssignedAtUtc { get; set; }

    public DateTime? ClosedAtUtc { get; set; }

    public DateTime LastUpdatedAtUtc { get; set; }
}
