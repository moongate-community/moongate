using MemoryPack;

namespace Moongate.UO.Data.Persistence.Entities;

[MemoryPackable(SerializeLayout.Explicit)]
public sealed partial class MobileResources
{
    [MemoryPackOrder(0)]
    public int Hits { get; set; }

    [MemoryPackOrder(1)]
    public int MaxHits { get; set; }

    [MemoryPackOrder(2)]
    public int Mana { get; set; }

    [MemoryPackOrder(3)]
    public int MaxMana { get; set; }

    [MemoryPackOrder(4)]
    public int Stamina { get; set; }

    [MemoryPackOrder(5)]
    public int MaxStamina { get; set; }
}
