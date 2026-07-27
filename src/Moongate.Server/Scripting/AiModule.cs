using Moongate.Core.Primitives;
using Moongate.Core.Types;
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

    [ScriptFunction("step", "Steps one tile in a compass direction (north/n, northeast/ne, …); false when blocked or unknown.")]
    public bool Step(string direction)
        => TryParseDirection(direction, out var parsed) && _actions.Step(parsed);

    [ScriptFunction("move_to", "Steps one tile toward (x, y) at the current z; false when blocked.")]
    public bool MoveTo(int x, int y)
        => _actions.MoveTo(x, y);

    private static bool TryParseDirection(string direction, out DirectionType parsed)
    {
        switch (direction?.Trim().ToLowerInvariant())
        {
            case "north":
            case "n":
                parsed = DirectionType.North;
                return true;
            case "northeast":
            case "ne":
                parsed = DirectionType.NorthEast;
                return true;
            case "east":
            case "e":
                parsed = DirectionType.East;
                return true;
            case "southeast":
            case "se":
                parsed = DirectionType.SouthEast;
                return true;
            case "south":
            case "s":
                parsed = DirectionType.South;
                return true;
            case "southwest":
            case "sw":
                parsed = DirectionType.SouthWest;
                return true;
            case "west":
            case "w":
                parsed = DirectionType.West;
                return true;
            case "northwest":
            case "nw":
                parsed = DirectionType.NorthWest;
                return true;
            default:
                parsed = default;
                return false;
        }
    }
}
