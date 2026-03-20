using Moongate.Generators.Annotations.Persistence;

namespace Moongate.UO.Data.Persistence.Entities;

[MoongatePersistedEntity]
public sealed class MobileModifiers
{
    [MoongatePersistedMember(0)]
    public int StrengthBonus { get; set; }

    [MoongatePersistedMember(1)]
    public int DexterityBonus { get; set; }

    [MoongatePersistedMember(2)]
    public int IntelligenceBonus { get; set; }

    [MoongatePersistedMember(3)]
    public int PhysicalResist { get; set; }

    [MoongatePersistedMember(4)]
    public int FireResist { get; set; }

    [MoongatePersistedMember(5)]
    public int ColdResist { get; set; }

    [MoongatePersistedMember(6)]
    public int PoisonResist { get; set; }

    [MoongatePersistedMember(7)]
    public int EnergyResist { get; set; }

    [MoongatePersistedMember(8)]
    public int HitChanceIncrease { get; set; }

    [MoongatePersistedMember(9)]
    public int DefenseChanceIncrease { get; set; }

    [MoongatePersistedMember(10)]
    public int DamageIncrease { get; set; }

    [MoongatePersistedMember(11)]
    public int SwingSpeedIncrease { get; set; }

    [MoongatePersistedMember(12)]
    public int SpellDamageIncrease { get; set; }

    [MoongatePersistedMember(13)]
    public int FasterCasting { get; set; }

    [MoongatePersistedMember(14)]
    public int FasterCastRecovery { get; set; }

    [MoongatePersistedMember(15)]
    public int LowerManaCost { get; set; }

    [MoongatePersistedMember(16)]
    public int LowerReagentCost { get; set; }

    [MoongatePersistedMember(17)]
    public int Luck { get; set; }

    [MoongatePersistedMember(18)]
    public int SpellChanneling { get; set; }
}
