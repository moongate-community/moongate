using MemoryPack;
using Moongate.Core.Attributes.Entities;
using Moongate.Core.Interfaces.Entities;
using Moongate.Core.Primitives;

namespace Moongate.Tests.Support.Persistence;

/// <summary>An entity that names its own collection, unlike <see cref="TestEntity"/>.</summary>
[PersistenceCollection("declared-items")]
[MemoryPackable(GenerateType.VersionTolerant)]
public partial class DeclaredCollectionEntity : IMoongateEntity
{
    [MemoryPackOrder(0)]
    public Serial Id { get; set; }

    [MemoryPackOrder(1)]
    public string Name { get; set; } = "";
}
