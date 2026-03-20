using MemoryPack;

namespace Moongate.UO.Data.Persistence.Entities;

[MemoryPackable(SerializeLayout.Explicit)]
public partial class ItemModifiers
{
    [MemoryPackOrder(0)]
    public int StrengthBonus { get; set; }
    [MemoryPackOrder(1)]
    public int DexterityBonus { get; set; }
    [MemoryPackOrder(2)]
    public int IntelligenceBonus { get; set; }
    [MemoryPackOrder(3)]
    public int PhysicalResist { get; set; }
    [MemoryPackOrder(4)]
    public int FireResist { get; set; }
    [MemoryPackOrder(5)]
    public int ColdResist { get; set; }
    [MemoryPackOrder(6)]
    public int PoisonResist { get; set; }
    [MemoryPackOrder(7)]
    public int EnergyResist { get; set; }
    [MemoryPackOrder(8)]
    public int HitChanceIncrease { get; set; }
    [MemoryPackOrder(9)]
    public int DefenseChanceIncrease { get; set; }
    [MemoryPackOrder(10)]
    public int DamageIncrease { get; set; }
    [MemoryPackOrder(11)]
    public int SwingSpeedIncrease { get; set; }
    [MemoryPackOrder(12)]
    public int SpellDamageIncrease { get; set; }
    [MemoryPackOrder(13)]
    public int FasterCasting { get; set; }
    [MemoryPackOrder(14)]
    public int FasterCastRecovery { get; set; }
    [MemoryPackOrder(15)]
    public int LowerManaCost { get; set; }
    [MemoryPackOrder(16)]
    public int LowerReagentCost { get; set; }
    [MemoryPackOrder(17)]
    public int Luck { get; set; }
    [MemoryPackOrder(18)]
    public int SpellChanneling { get; set; }
    [MemoryPackOrder(19)]
    public int UsesRemaining { get; set; }
}
