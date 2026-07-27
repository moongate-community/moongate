namespace Moongate.Core.Interfaces;

/// <summary>
/// Guards world mutations against running off the single game-loop thread. Injected into the
/// mutation services so the check sits on the real write paths — reachable from C#, plugins and
/// jobs — not only on the Lua-facing modules.
/// </summary>
public interface ILoopAffinity
{
    /// <summary>
    /// Asserts the caller is on the game-loop thread. Off it, throws when strict loop affinity is
    /// configured, otherwise logs a warning naming <paramref name="operation" />.
    /// </summary>
    void AssertOnLoop(string operation);
}
