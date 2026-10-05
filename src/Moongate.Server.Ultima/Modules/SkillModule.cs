using Moongate.Core.Primitives;
using Moongate.Core.Utils;
using Moongate.Scripting.Attributes.Scripts;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Modules;

/// <summary>
///     The <c>skill</c> Lua module: tries a mobile at a skill, which may rise with the try;
///     <c>if skill.check(user, "hiding", 0, 100) then ... end</c>.
/// </summary>
[ScriptModule("skill", "Tries a mobile at a skill, which may rise with the try.")]
public sealed class SkillModule
{
    private readonly ISkillService _skills;
    private readonly IMobileService _mobiles;

    public SkillModule(ISkillService skills, IMobileService mobiles)
    {
        _skills = skills;
        _mobiles = mobiles;
    }

    /// <summary>
    ///     Tries the mobile at a skill, between the points at which the task can just be begun and those at which it
    ///     never fails; <c>skill.check(user, "lockpicking", 30, 80)</c>.
    /// </summary>
    [ScriptFunction(helpText: "Tries the mobile at a skill, named as in data/skills.toml (hiding, animal_lore): min is the points (50.5) at which the task can just be begun, max those at which it never fails. Below min it fails, at max or above it succeeds, and neither teaches; in between the chance grows in a line, and the try, passed or failed, may raise the skill of a player by a tenth of a point, more often with room under the caps and for a hard task. False also for an unknown skill or a mobile not in the world.")]
    public bool Check(long mobile, string skill, double min, double max)
    {
        return mobile is > 0 and <= uint.MaxValue &&
               EnumNameUtils.TryParse<SkillType>(skill, out var type) &&
               Enum.IsDefined(type) &&
               _mobiles.TryGet(new Serial((uint)mobile), out var who) &&
               _mobiles.IsInWorld(who.Id) &&
               _skills.Check(who, type, min, max);
    }
}
