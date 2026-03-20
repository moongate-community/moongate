using MemoryPack;

namespace Moongate.UO.Data.Persistence.Entities;

[MemoryPackable(SerializeLayout.Explicit)]
public partial class ItemCombatStats
{
    [MemoryPackOrder(0)]
    public int MinStrength { get; set; }
    [MemoryPackOrder(1)]
    public int MinDexterity { get; set; }
    [MemoryPackOrder(2)]
    public int MinIntelligence { get; set; }
    [MemoryPackOrder(3)]
    public int DamageMin { get; set; }
    [MemoryPackOrder(4)]
    public int DamageMax { get; set; }
    [MemoryPackOrder(5)]
    public int Defense { get; set; }
    [MemoryPackOrder(6)]
    public int AttackSpeed { get; set; }
    [MemoryPackOrder(7)]
    public int RangeMin { get; set; }
    [MemoryPackOrder(8)]
    public int RangeMax { get; set; }
    [MemoryPackOrder(9)]
    public int MaxDurability { get; set; }
    [MemoryPackOrder(10)]
    public int CurrentDurability { get; set; }
}
