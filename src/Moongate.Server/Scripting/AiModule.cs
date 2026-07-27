using Moongate.Core.Primitives;
using Moongate.Server.Abstractions.Interfaces.AI;
using SquidStd.Scripting.Lua.Attributes.Scripts;

namespace Moongate.Server.Scripting;

/// <summary>
/// Exposes the active NPC brain's actions to Lua. Every function operates on the mobile of the
/// current brain tick (set by the scheduler); calling one outside a tick raises a Lua error.
/// </summary>
[ScriptModule("ai", "Act as the current NPC brain: speak, move, engage.")]
public sealed class AiModule
{
    private readonly IAiActionService _actions;

    public AiModule(IAiActionService actions)
    {
        _actions = actions;
    }

    [ScriptFunction("say", "Speaks as the current NPC; false when the text is blank or too long.")]
    public bool Say(string text)
        => _actions.Say(text);

    [ScriptFunction("patrol", "Wanders within the home leash, else steps toward home; false off the home map.")]
    public bool Patrol()
        => _actions.Patrol();

    [ScriptFunction("return_home", "Steps one tile toward home; false off the home map.")]
    public bool ReturnHome()
        => _actions.ReturnHome();

    [ScriptFunction("move_toward", "Steps toward the perceived target; false when not perceivable.")]
    public bool MoveToward(uint target)
        => _actions.MoveToward((Serial)target);

    [ScriptFunction("move_away", "Steps away from the perceived target; false when not perceivable.")]
    public bool MoveAway(uint target)
        => _actions.MoveAway((Serial)target);

    [ScriptFunction("engage", "Sets combat posture on the perceived target; false when not perceivable.")]
    public bool Engage(uint target)
        => _actions.Engage((Serial)target);

    [ScriptFunction("clear_target", "Clears combat posture on the current NPC.")]
    public bool ClearTarget()
        => _actions.ClearTarget();
}
