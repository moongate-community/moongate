using Moongate.Generators.Annotations.Persistence;

namespace Moongate.UO.Data.Persistence.Entities;

[MoongatePersistedEntity]
public sealed class MobileModifierCaps
{
    [MoongatePersistedMember(0)]
    public int PhysicalResist { get; set; }

    [MoongatePersistedMember(1)]
    public int FireResist { get; set; }

    [MoongatePersistedMember(2)]
    public int ColdResist { get; set; }

    [MoongatePersistedMember(3)]
    public int PoisonResist { get; set; }

    [MoongatePersistedMember(4)]
    public int EnergyResist { get; set; }

    [MoongatePersistedMember(5)]
    public int DefenseChanceIncrease { get; set; }
}
