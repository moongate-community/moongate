using Moongate.Server.Data.Interaction;
using Moongate.Server.Interfaces.Services.Interaction;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Skills;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Services.Interaction;

public sealed class StatGainService : IStatGainService
{
    private const int GainAmount = 1;
    private const double StatGainChance = 0.05;
    private const double PrimaryStatBias = 0.75;
    private const int MinimumLowerableStatValue = 10;

    private readonly Func<double> _attemptRollProvider;
    private readonly Func<double> _selectionRollProvider;

    public StatGainService()
        : this(static () => Random.Shared.NextDouble(), static () => Random.Shared.NextDouble())
    {
    }

    internal StatGainService(Func<double> attemptRollProvider, Func<double> selectionRollProvider)
    {
        ArgumentNullException.ThrowIfNull(attemptRollProvider);
        ArgumentNullException.ThrowIfNull(selectionRollProvider);
        _attemptRollProvider = attemptRollProvider;
        _selectionRollProvider = selectionRollProvider;
    }

    public StatGainResult TryApply(UOMobileEntity mobile, UOSkillName skillName)
    {
        ArgumentNullException.ThrowIfNull(mobile);

        if (_attemptRollProvider() > StatGainChance)
        {
            return new(false, null, null);
        }

        if (!TryResolveSkillInfo(skillName, out var skillInfo))
        {
            return new(false, null, null);
        }

        var preferredStat = _selectionRollProvider() <= PrimaryStatBias
            ? skillInfo.PrimaryStat
            : skillInfo.SecondaryStat;
        var fallbackStat = preferredStat == skillInfo.PrimaryStat ? skillInfo.SecondaryStat : skillInfo.PrimaryStat;

        if (TryIncreaseStat(mobile, preferredStat, out var loweredPrimary))
        {
            return new(true, preferredStat, loweredPrimary);
        }

        if (fallbackStat == preferredStat)
        {
            return new(false, null, null);
        }

        if (TryIncreaseStat(mobile, fallbackStat, out var loweredSecondary))
        {
            return new(true, fallbackStat, loweredSecondary);
        }

        return new(false, null, null);
    }

    private static bool TryResolveSkillInfo(UOSkillName skillName, out SkillInfo skillInfo)
    {
        foreach (var entry in SkillInfo.Table)
        {
            if (entry.SkillID == (int)skillName)
            {
                skillInfo = entry;
                return true;
            }
        }

        skillInfo = default!;
        return false;
    }

    private static bool TryIncreaseStat(UOMobileEntity mobile, Stat stat, out Stat? loweredStat)
    {
        loweredStat = null;

        if (mobile.GetStatLock(stat) != UOSkillLock.Up)
        {
            return false;
        }

        if (mobile.GetTotalBaseStats() + GainAmount > mobile.StatCap)
        {
            loweredStat = TryLowerDownLockedStat(mobile, stat);

            if (loweredStat is null)
            {
                return false;
            }
        }

        SetStatValue(mobile, stat, GetStatValue(mobile, stat) + GainAmount);
        mobile.RecalculateMaxStats();

        return true;
    }

    private static Stat? TryLowerDownLockedStat(UOMobileEntity mobile, Stat excludedStat)
    {
        foreach (var candidate in Enum.GetValues<Stat>())
        {
            if (candidate == excludedStat)
            {
                continue;
            }

            if (mobile.GetStatLock(candidate) != UOSkillLock.Down)
            {
                continue;
            }

            var currentValue = GetStatValue(mobile, candidate);

            if (currentValue <= MinimumLowerableStatValue)
            {
                continue;
            }

            SetStatValue(mobile, candidate, currentValue - GainAmount);
            mobile.RecalculateMaxStats();

            return candidate;
        }

        return null;
    }

    private static int GetStatValue(UOMobileEntity mobile, Stat stat)
        => stat switch
        {
            Stat.Strength => mobile.Strength,
            Stat.Dexterity => mobile.Dexterity,
            Stat.Intelligence => mobile.Intelligence,
            _ => throw new ArgumentOutOfRangeException(nameof(stat), stat, null)
        };

    private static void SetStatValue(UOMobileEntity mobile, Stat stat, int value)
    {
        switch (stat)
        {
            case Stat.Strength:
                mobile.Strength = value;
                break;
            case Stat.Dexterity:
                mobile.Dexterity = value;
                break;
            case Stat.Intelligence:
                mobile.Intelligence = value;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(stat), stat, null);
        }
    }
}
