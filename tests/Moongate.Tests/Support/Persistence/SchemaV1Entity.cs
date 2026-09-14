using MemoryPack;
using Moongate.Core.Interfaces.Entities;
using Moongate.Core.Primitives;

namespace Moongate.Tests.Support.Persistence;

[MemoryPackable(GenerateType.VersionTolerant)]
public partial class SchemaV1Entity : IMoongateEntity
{
    [MemoryPackOrder(0)]
    public Serial Id { get; set; }

    [MemoryPackOrder(1)]
    public string Name { get; set; } = "";
}
