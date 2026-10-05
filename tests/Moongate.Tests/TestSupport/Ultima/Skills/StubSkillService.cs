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

    public List<(MobileEntity Mobile, SkillType Skill, double Min, double Max)> Checks { get; } = [];

    public bool Check(MobileEntity mobile, SkillType skill, double min, double max)
    {
        Checks.Add((mobile, skill, min, max));

        return Result;
    }

    public int Total(MobileEntity mobile)
    {
        return 0;
    }
}
