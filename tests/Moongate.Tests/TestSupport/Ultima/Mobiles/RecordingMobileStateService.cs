using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Ultima.Data.Mobiles;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Ultima.Types;

namespace Moongate.Tests.TestSupport.Ultima.Mobiles;

/// <summary>
///     Records what it is asked to change and answers <see cref="Result" />; the skills it gives are those of
///     <see cref="Skills" />, else zero.
/// </summary>
public sealed class RecordingMobileStateService : IMobileStateService
{
    public bool Result { get; set; } = true;

    public List<MobileSkill> Skills { get; } = [];

    public List<(MobileEntity Mobile, MobileStatsChange Change)> Stats { get; } = [];

    public List<(MobileEntity Mobile, SkillType Skill, int Value, int? Cap)> SkillsSet { get; } = [];

    public List<(MobileEntity Mobile, string Name)> Names { get; } = [];

    public List<(MobileEntity Mobile, int? Body, int? Hue)> Looks { get; } = [];

    public bool SetStats(MobileEntity mobile, MobileStatsChange change)
    {
        Stats.Add((mobile, change));

        return Result;
    }

    public MobileSkill GetSkill(MobileEntity mobile, SkillType skill)
    {
        return Skills.FirstOrDefault(known => known.Skill == skill) ?? new MobileSkill { Skill = skill };
    }

    public IReadOnlyList<MobileSkill> GetSkills(MobileEntity mobile)
    {
        return Enum.GetValues<SkillType>().Select(skill => GetSkill(mobile, skill)).ToList();
    }

    public bool SetSkill(MobileEntity mobile, SkillType skill, int value, int? cap = null)
    {
        SkillsSet.Add((mobile, skill, value, cap));

        return Result;
    }

    public bool SetName(MobileEntity mobile, string name)
    {
        Names.Add((mobile, name));

        return Result;
    }

    public bool SetLooks(MobileEntity mobile, int? body, int? hue)
    {
        Looks.Add((mobile, body, hue));

        return Result;
    }

    public void SendStatus(GameSession session, MobileEntity target)
    {
    }

    public void SendSkills(GameSession session, MobileEntity character)
    {
    }
}
