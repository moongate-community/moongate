using Moongate.Scripting.Data.Scripts;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     Calls the script of a skill a player uses, the global table named after the skill, defined by
///     <c>scripts/skills/&lt;skill&gt;.lua</c>: <c>hiding</c>, <c>animal_lore</c>. Called on the game loop; nothing
///     runs
///     before the scripts are loaded or once they stop.
/// </summary>
public interface ISkillScriptService
{
    /// <summary>
    ///     Calls <c>on_use(user)</c> of the skill's script; <see cref="ScriptResult.Missing" /> when the skill has no
    ///     script or the script lacks it: a skill that cannot be used directly.
    /// </summary>
    ScriptResult Use(SkillType skill, MobileEntity user);

    /// <summary>
    ///     Calls another function of the script of a skill, such as the <c>on_snoop</c> of Snooping, which is not used
    ///     from the skill window; <see cref="ScriptResult.Missing" /> when the skill has no script or function.
    /// </summary>
    ScriptResult Call(SkillType skill, string function, params object?[] args);
}
