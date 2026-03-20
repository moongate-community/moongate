using Moongate.Generators.Annotations.Persistence;
using Moongate.UO.Data.Types;

namespace Moongate.UO.Data.Persistence.Entities;

[MoongatePersistedEntity]
public sealed class PersistedMobileSoundEntry
{
    [MoongatePersistedMember(0)]
    public MobileSoundType SoundType { get; set; }

    [MoongatePersistedMember(1)]
    public int SoundId { get; set; }
}
