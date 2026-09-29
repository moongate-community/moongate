using Moongate.Scripting.Data.Scripts;
using Moongate.Server.Ultima.Entities.World;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     Calls the functions of an NPC's mobile script, the global table its template names with <c>script_id</c>, with
///     the NPC's serial first. Called on the game loop; nothing runs before the scripts are loaded or once they stop.
/// </summary>
public interface INpcScriptService
{
    /// <summary>
    ///     Calls <paramref name="function" /> now; <see cref="ScriptResult.Missing" /> when the NPC has no script or the
    ///     script lacks it. Never from inside a running script: use <see cref="Queue" /> there.
    /// </summary>
    ScriptResult Run(MobileEntity npc, string function, params object?[] args);

    /// <summary>
    ///     Calls <paramref name="function" /> on the next turn of the game loop, after the current work; safe from inside a
    ///     running script, as when <c>npc.step</c> brings an NPC near another.
    /// </summary>
    void Queue(MobileEntity npc, string function, params object?[] args);
}
