using MemoryPack;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Types;

namespace Moongate.UO.Data.Persistence.Entities;

[MemoryPackable(SerializeLayout.Explicit)]
public sealed partial class HelpTicketEntity
{
    [MemoryPackOrder(0)]
    public Serial Id { get; set; }

    [MemoryPackOrder(1)]
    public Serial SenderCharacterId { get; set; }

    [MemoryPackOrder(2)]
    public Serial SenderAccountId { get; set; }

    [MemoryPackOrder(3)]
    public HelpTicketCategory Category { get; set; }

    [MemoryPackOrder(4)]
    public string Message { get; set; } = string.Empty;

    [MemoryPackOrder(5)]
    public int MapId { get; set; }

    [MemoryPackOrder(6)]
    public Point3D Location { get; set; }

    [MemoryPackOrder(7)]
    public HelpTicketStatus Status { get; set; }

    [MemoryPackOrder(8)]
    public Serial AssignedToCharacterId { get; set; }

    [MemoryPackOrder(9)]
    public Serial AssignedToAccountId { get; set; }

    [MemoryPackOrder(10)]
    public DateTime CreatedAtUtc { get; set; }

    [MemoryPackOrder(11)]
    public DateTime? AssignedAtUtc { get; set; }

    [MemoryPackOrder(12)]
    public DateTime? ClosedAtUtc { get; set; }

    [MemoryPackOrder(13)]
    public DateTime LastUpdatedAtUtc { get; set; }
}
