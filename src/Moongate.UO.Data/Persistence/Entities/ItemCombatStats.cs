using Moongate.Generators.Annotations.Persistence;

namespace Moongate.UO.Data.Persistence.Entities;

[MoongatePersistedEntity]
public partial class ItemCombatStats
{
    [MoongatePersistedMember(0)]
    public int MinStrength { get; set; }

    [MoongatePersistedMember(1)]
    public int MinDexterity { get; set; }

    [MoongatePersistedMember(2)]
    public int MinIntelligence { get; set; }

    [MoongatePersistedMember(3)]
    public int DamageMin { get; set; }

    [MoongatePersistedMember(4)]
    public int DamageMax { get; set; }

    [MoongatePersistedMember(5)]
    public int Defense { get; set; }

    [MoongatePersistedMember(6)]
    public int AttackSpeed { get; set; }

    [MoongatePersistedMember(7)]
    public int RangeMin { get; set; }

    [MoongatePersistedMember(8)]
    public int RangeMax { get; set; }

    [MoongatePersistedMember(9)]
    public int MaxDurability { get; set; }

    [MoongatePersistedMember(10)]
    public int CurrentDurability { get; set; }
}
