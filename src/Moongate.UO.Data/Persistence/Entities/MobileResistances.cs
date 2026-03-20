using Moongate.Generators.Annotations.Persistence;

namespace Moongate.UO.Data.Persistence.Entities;

[MoongatePersistedEntity]
public sealed class MobileResistances
{
    [MoongatePersistedMember(0)]
    public int Physical { get; set; }

    [MoongatePersistedMember(1)]
    public int Fire { get; set; }

    [MoongatePersistedMember(2)]
    public int Cold { get; set; }

    [MoongatePersistedMember(3)]
    public int Poison { get; set; }

    [MoongatePersistedMember(4)]
    public int Energy { get; set; }
}
