using MemoryPack;

namespace Moongate.Tests.Persistence.Support;

[MemoryPackable(SerializeLayout.Explicit)]
public sealed partial class TestRegisteredEntity
{
    [MemoryPackOrder(0)]
    public int Id { get; set; }

    [MemoryPackOrder(1)]
    public string Name { get; set; } = string.Empty;
}
