using Moongate.Generators.Annotations.Persistence;
using Moongate.UO.Data.Types;

namespace Moongate.UO.Data.Persistence.Entities;

[MoongatePersistedEntity]
public sealed class PersistedEquippedItemEntry
{
    [MoongatePersistedMember(0)]
    public ItemLayerType Layer { get; set; }

    [MoongatePersistedMember(1)]
    public uint ItemId { get; set; }
}
