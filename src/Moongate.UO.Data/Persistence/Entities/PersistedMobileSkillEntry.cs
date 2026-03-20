using Moongate.Generators.Annotations.Persistence;
using Moongate.UO.Data.Skills;
using Moongate.UO.Data.Types;

namespace Moongate.UO.Data.Persistence.Entities;

[MoongatePersistedEntity]
public sealed class PersistedMobileSkillEntry
{
    [MoongatePersistedMember(0)]
    public UOSkillName SkillId { get; set; }

    [MoongatePersistedMember(1)]
    public double Value { get; set; }

    [MoongatePersistedMember(2)]
    public double Base { get; set; }

    [MoongatePersistedMember(3)]
    public int Cap { get; set; }

    [MoongatePersistedMember(4)]
    public UOSkillLock Lock { get; set; }
}
