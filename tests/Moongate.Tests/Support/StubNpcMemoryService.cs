using Moongate.Core.Primitives;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Data.AI;
using Moongate.Server.Abstractions.Interfaces.AI;

namespace Moongate.Tests.Support;

/// <summary>No-op NPC memory service for scheduler/integration tests that don't exercise memory.</summary>
public sealed class StubNpcMemoryService : INpcMemoryService
{
    private static readonly IReadOnlyDictionary<string, NpcMemoryValue> Empty = new Dictionary<string, NpcMemoryValue>();

    public IDisposable Begin(BrainContext context)
        => new NoopScope();

    public bool Set(string key, NpcMemoryValue value)
        => true;

    public NpcMemoryValue? Get(string key)
        => null;

    public bool Delete(string key)
        => false;

    public IReadOnlyDictionary<string, NpcMemoryValue> All()
        => Empty;

    public void Forget(Serial mobileId)
    {
    }

    private sealed class NoopScope : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
