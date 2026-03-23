using MemoryPack;

namespace Moongate.UO.Data.Persistence.Entities;

[MemoryPackable(SerializeLayout.Explicit)]
public sealed partial class MobileStats
{
    [MemoryPackOrder(0)]
    public int Strength { get; set; }

    [MemoryPackOrder(1)]
    public int Dexterity { get; set; }

    [MemoryPackOrder(2)]
    public int Intelligence { get; set; }
}
