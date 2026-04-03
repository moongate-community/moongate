using MemoryPack;
using Moongate.UO.Data.Types;

namespace Moongate.UO.Data.Persistence.Entities;

[MemoryPackable(SerializeLayout.Explicit)]
public sealed partial class QuestProgressEntity
{
    [MemoryPackOrder(0)]
    public string QuestId { get; set; } = string.Empty;

    [MemoryPackOrder(1)]
    public QuestProgressStatusType Status { get; set; }

    [MemoryPackOrder(2)]
    public DateTime AcceptedAtUtc { get; set; }

    [MemoryPackOrder(3)]
    public DateTime? CompletedAtUtc { get; set; }

    [MemoryPackOrder(4)]
    public List<QuestObjectiveProgressEntity> Objectives { get; set; } = [];
}
