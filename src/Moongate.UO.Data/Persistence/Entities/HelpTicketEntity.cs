using Moongate.Generators.Annotations.Persistence;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Types;

namespace Moongate.UO.Data.Persistence.Entities;

public sealed partial class HelpTicketEntity
{
    [MoongatePersistedMember(0)]
    public Serial Id { get; set; }

    [MoongatePersistedMember(1)]
    public Serial SenderCharacterId { get; set; }

    [MoongatePersistedMember(2)]
    public Serial SenderAccountId { get; set; }

    [MoongatePersistedMember(3)]
    public HelpTicketCategory Category { get; set; }

    [MoongatePersistedMember(4)]
    public string Message { get; set; } = string.Empty;

    [MoongatePersistedMember(5)]
    public int MapId { get; set; }

    public Point3D Location { get; set; }

    [MoongatePersistedMember(7)]
    public HelpTicketStatus Status { get; set; }

    [MoongatePersistedMember(8)]
    public Serial AssignedToCharacterId { get; set; }

    [MoongatePersistedMember(9)]
    public Serial AssignedToAccountId { get; set; }

    [MoongatePersistedMember(10)]
    public DateTime CreatedAtUtc { get; set; }

    [MoongatePersistedMember(11)]
    public DateTime? AssignedAtUtc { get; set; }

    [MoongatePersistedMember(12)]
    public DateTime? ClosedAtUtc { get; set; }

    [MoongatePersistedMember(13)]
    public DateTime LastUpdatedAtUtc { get; set; }
}
