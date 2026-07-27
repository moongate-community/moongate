using Moongate.Core.Primitives;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Data.AI;

namespace Moongate.Server.Abstractions.Interfaces.AI;

/// <summary>Durable per-NPC key/value memory for the mobile of the active brain tick.</summary>
public interface INpcMemoryService
{
    /// <summary>Sets the ambient brain context for the current tick; disposing restores the previous one.</summary>
    IDisposable Begin(BrainContext context);

    /// <summary>Stores a value under a key; false when the mobile is unknown or a bound is exceeded.</summary>
    bool Set(string key, NpcMemoryValue value);

    /// <summary>Returns the stored value for a key, or null when absent.</summary>
    NpcMemoryValue? Get(string key);

    /// <summary>Removes a key; true when it existed.</summary>
    bool Delete(string key);

    /// <summary>Returns every stored key/value for the active mobile.</summary>
    IReadOnlyDictionary<string, NpcMemoryValue> All();

    /// <summary>Permanently drops all memory for a mobile (used when it is deleted).</summary>
    void Forget(Serial mobileId);
}
