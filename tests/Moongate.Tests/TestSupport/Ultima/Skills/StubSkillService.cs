using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Ultima.Types;

namespace Moongate.Tests.TestSupport.Ultima.Skills;

/// <summary>
///     Records the tries it is asked for and answers <see cref="Result" />.
/// </summary>
public sealed class StubSkillService : ISkillService
{
    public bool Result { get; set; } = true;

    /// <summary>
    ///     When set, the answer for a skill, in place of <see cref="Result" />.
    /// </summary>
    public Func<SkillType, bool>? ResultBySkill { get; set; }

    public List<(MobileEntity Mobile, SkillType Skill, double Min, double Max)> Checks { get; } = [];

    public bool Check(MobileEntity mobile, SkillType skill, double min, double max)
    {
        Checks.Add((mobile, skill, min, max));

        return ResultBySkill?.Invoke(skill) ?? Result;
    }

    public List<(MobileEntity Mobile, SkillType Skill, double Chance)> Chances { get; } = [];

    /// <summary>
    ///     What <see cref="CheckChance" /> answers.
    /// </summary>
    public bool ChanceResult { get; set; } = true;

    public bool CheckChance(MobileEntity mobile, SkillType skill, double chance)
    {
        Chances.Add((mobile, skill, chance));

        return ChanceResult;
    }

    public int Total(MobileEntity mobile)
    {
        return 0;
    }
}
