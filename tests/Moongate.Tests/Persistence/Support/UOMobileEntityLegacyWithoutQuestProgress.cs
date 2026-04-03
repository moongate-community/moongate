using MemoryPack;
using Moongate.UO.Data.Ids;

namespace Moongate.Tests.Persistence.Support;

[MemoryPackable(SerializeLayout.Explicit)]
public sealed partial class UOMobileEntityLegacyWithoutQuestProgress
{
    [MemoryPackOrder(0)]
    public Serial Id { get; set; }
}
