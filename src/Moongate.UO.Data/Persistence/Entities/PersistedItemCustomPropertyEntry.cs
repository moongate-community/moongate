using Moongate.Generators.Annotations.Persistence;

namespace Moongate.UO.Data.Persistence.Entities;

[MoongatePersistedEntity]
public sealed class PersistedItemCustomPropertyEntry
{
    [MoongatePersistedMember(0)]
    public string Key { get; set; } = string.Empty;

    [MoongatePersistedMember(1)]
    public ItemCustomProperty Property { get; set; } = new();
}
