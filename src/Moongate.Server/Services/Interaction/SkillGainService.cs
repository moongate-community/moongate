using Moongate.Server.Data.Interaction;
using Moongate.Server.Interfaces.Services.Interaction;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Skills;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Services.Interaction;

public sealed class SkillGainService : ISkillGainService
{
    private const int GainAmount = 1;
    private readonly Func<double> _nextDouble;
    private readonly ISkillAntiMacroService _skillAntiMacroService;
    private readonly IStatGainService _statGainService;

    public SkillGainService(ISkillAntiMacroService skillAntiMacroService, IStatGainService statGainService)
        : this(static () => Random.Shared.NextDouble(), skillAntiMacroService, statGainService) { }

    internal SkillGainService(Func<double> nextDouble)
        : this(
            nextDouble,
            new SkillAntiMacroService(static () => DateTime.UtcNow),
            new StatGainService(static () => 1.0, static () => 0.0)
        ) { }

    internal SkillGainService(
        Func<double> nextDouble,
        ISkillAntiMacroService skillAntiMacroService,
        IStatGainService statGainService
    )
    {
        ArgumentNullException.ThrowIfNull(nextDouble);
        ArgumentNullException.ThrowIfNull(skillAntiMacroService);
        ArgumentNullException.ThrowIfNull(statGainService);
        _nextDouble = nextDouble;
        _skillAntiMacroService = skillAntiMacroService;
        _statGainService = statGainService;
    }

    public SkillGainResult TryGain(
        UOMobileEntity mobile,
        UOSkillName skillName,
        double successChance,
        bool wasSuccessful,
        SkillGainContext? context = null
    )
    {
        ArgumentNullException.ThrowIfNull(mobile);

        var skill = mobile.GetSkill(skillName);

        if (skill is null || skill.Lock != UOSkillLock.Up)
        {
            return new(skillName, false, null);
        }

        var currentBase = (int)Math.Round(skill.Base);
        var skillCap = Math.Max(1, skill.Cap);

        if (currentBase >= skillCap)
        {
            return new(skillName, false, null);
        }

        if (!_skillAntiMacroService.AllowGain(mobile, skillName, context))
        {
            return new(skillName, false, null);
        }

        var normalizedSuccessChance = Math.Clamp(successChance, 0.0, 1.0);
        var capRoomFactor = (skillCap - currentBase) / (double)skillCap;
        var difficultyFactor = 1.0 - normalizedSuccessChance;
        var successModifier = wasSuccessful ? 0.25 : 0.10;
        var gainFactor = skill.Skill?.GainFactor ?? ResolveGainFactor(skillName);
        var gainChance = Math.Clamp((capRoomFactor + difficultyFactor + successModifier) / 3.0 * gainFactor, 0.01, 0.95);

        if (_nextDouble() > gainChance)
        {
            return new(skillName, false, null);
        }

        UOSkillName? loweredSkillName = null;

        if (mobile.GetTotalSkillBaseFixedPoint() + GainAmount > mobile.TotalSkillCapFixedPoint)
        {
            loweredSkillName = TryLowerDownLockedSkill(mobile, skillName, GainAmount);

            if (loweredSkillName is null)
            {
                return new(skillName, false, null);
            }
        }

        skill.Base = Math.Min(currentBase + GainAmount, skillCap);
        skill.Value = skill.Base;
        _ = _statGainService.TryApply(mobile, skillName);

        return new(skillName, true, loweredSkillName);
    }

    private static double ResolveGainFactor(UOSkillName skillName)
    {
        foreach (var skillInfo in SkillInfo.Table)
        {
            if (skillInfo.SkillID == (int)skillName)
            {
                return skillInfo.GainFactor;
            }
        }

        return 1.0;
    }

    private static UOSkillName? TryLowerDownLockedSkill(UOMobileEntity mobile, UOSkillName excludedSkill, int amount)
    {
        foreach (var skillEntry in mobile.Skills.OrderBy(static pair => (int)pair.Key))
        {
            if (skillEntry.Key == excludedSkill)
            {
                continue;
            }

            var entry = skillEntry.Value;
            var currentBase = (int)Math.Round(entry.Base);

            if (entry.Lock != UOSkillLock.Down || currentBase < amount)
            {
                continue;
            }

            entry.Base = currentBase - amount;
            entry.Value = entry.Base;

            return skillEntry.Key;
        }

        return null;
    }
}
