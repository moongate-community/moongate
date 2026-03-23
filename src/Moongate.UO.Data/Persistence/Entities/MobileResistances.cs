using MemoryPack;

namespace Moongate.UO.Data.Persistence.Entities;

[MemoryPackable(SerializeLayout.Explicit)]
public sealed partial class MobileResistances
{
    [MemoryPackOrder(0)]
    public int Physical { get; set; }

    [MemoryPackOrder(1)]
    public int Fire { get; set; }

    [MemoryPackOrder(2)]
    public int Cold { get; set; }

    [MemoryPackOrder(3)]
    public int Poison { get; set; }

    [MemoryPackOrder(4)]
    public int Energy { get; set; }
}
