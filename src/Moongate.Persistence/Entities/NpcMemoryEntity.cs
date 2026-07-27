using Moongate.Core.Primitives;
using Moongate.Persistence.Interfaces;

namespace Moongate.Persistence.Entities;

public sealed class NpcMemoryEntity : ISerialIdEntity
{
    public Serial Id { get; set; }

    public Dictionary<string, NpcMemoryValue> Memory { get; set; } = new();
}
