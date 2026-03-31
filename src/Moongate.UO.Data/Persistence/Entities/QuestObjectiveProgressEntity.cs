using MemoryPack;

namespace Moongate.UO.Data.Persistence.Entities;

[MemoryPackable(SerializeLayout.Explicit)]
public sealed partial class QuestObjectiveProgressEntity
{
    [MemoryPackOrder(0)]
    public int ObjectiveIndex { get; set; }

    [MemoryPackOrder(1)]
    public int CurrentAmount { get; set; }

    [MemoryPackOrder(2)]
    public bool IsCompleted { get; set; }
}
