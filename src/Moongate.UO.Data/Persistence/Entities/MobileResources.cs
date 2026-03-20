using Moongate.Generators.Annotations.Persistence;

namespace Moongate.UO.Data.Persistence.Entities;

[MoongatePersistedEntity]
public sealed class MobileResources
{
    [MoongatePersistedMember(0)]
    public int Hits { get; set; }

    [MoongatePersistedMember(1)]
    public int MaxHits { get; set; }

    [MoongatePersistedMember(2)]
    public int Mana { get; set; }

    [MoongatePersistedMember(3)]
    public int MaxMana { get; set; }

    [MoongatePersistedMember(4)]
    public int Stamina { get; set; }

    [MoongatePersistedMember(5)]
    public int MaxStamina { get; set; }
}
