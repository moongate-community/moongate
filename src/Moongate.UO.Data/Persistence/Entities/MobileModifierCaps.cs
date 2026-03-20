using MemoryPack;

namespace Moongate.UO.Data.Persistence.Entities;

[MemoryPackable(SerializeLayout.Explicit)]
public sealed partial class MobileModifierCaps
{
    [MemoryPackOrder(0)]
    public int PhysicalResist { get; set; }
    [MemoryPackOrder(1)]
    public int FireResist { get; set; }
    [MemoryPackOrder(2)]
    public int ColdResist { get; set; }
    [MemoryPackOrder(3)]
    public int PoisonResist { get; set; }
    [MemoryPackOrder(4)]
    public int EnergyResist { get; set; }
    [MemoryPackOrder(5)]
    public int DefenseChanceIncrease { get; set; }
}
