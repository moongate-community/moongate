using MessagePack;

namespace Moongate.Persistence.Data.Persistence;

[MessagePackObject(true)]
public sealed class HelpTicketSnapshot
{
    public uint Id { get; set; }

    public uint SenderCharacterId { get; set; }

    public uint SenderAccountId { get; set; }

    public byte Category { get; set; }

    public string Message { get; set; } = string.Empty;

    public int MapId { get; set; }

    public int X { get; set; }

    public int Y { get; set; }

    public int Z { get; set; }

    public byte Status { get; set; }

    public uint AssignedToCharacterId { get; set; }

    public uint AssignedToAccountId { get; set; }

    public long CreatedAtUtcTicks { get; set; }

    public long? AssignedAtUtcTicks { get; set; }

    public long? ClosedAtUtcTicks { get; set; }

    public long LastUpdatedAtUtcTicks { get; set; }
}
