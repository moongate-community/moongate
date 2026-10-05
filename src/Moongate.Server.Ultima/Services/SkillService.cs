using Moongate.Server.Ultima.Interfaces.Loaders;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.Mobiles;
using Moongate.Server.Ultima.Data.Skills;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     The skill check and the gain of ModernUO and ServUO, with the rule of the classic game that a failure teaches
///     too, less than a success; and the stats a successful check can raise, as ModernUO's classic rule. The value of a
///     skill is its base: stats add nothing to it yet.
/// </summary>
public sealed class SkillService : ISkillService
{
    // In tenths of a point, as the skills are kept.
    private const int Tenths = 10;
    private const int Step = 1;

    /// <summary>
    ///     Below this many tenths a skill always rises when it is used, and by up to four tenths.
    /// </summary>
    public const int BeginnerBase = 100;

    private const double SuccessWeight = 0.5;
    private const double FailureWeight = 0.2;
    private const double LeastGainChance = 0.01;

    // A stat gain of 33.3 in skills.toml is a sure thing at each try.
    private const double StatGainScale = 33.3;

    // The least a stat can be lowered to, so one that is locked down never reaches nothing.
    private const int LeastStat = 10;

    private readonly IMobileStateService _state;
    private readonly SkillsConfig _config;
    private readonly Random _random;
    private readonly TimeProvider _time;
    private readonly Lazy<Dictionary<SkillType, SkillContent>> _skills;

    public SkillService(
        IMobileStateService state,
        IDataLoaderService data,
        SkillsConfig config,
        Random? random = null,
        TimeProvider? time = null
    )
    {
        _state = state;
        _config = config;
        _random = random ?? Random.Shared;
        _time = time ?? TimeProvider.System;
        _skills = new(() => data.GetEntities<SkillContent>().ToDictionary(skill => skill.Id));
    }

    public bool Check(MobileEntity mobile, SkillType skill, double min, double max)
    {
        if (!Enum.IsDefined(skill))
        {
            return false;
        }

        // One it never trained is at 0, going up, with the usual cap.
        var known = mobile.Skills.FirstOrDefault(entry => entry.Skill == skill) ?? new MobileSkill { Skill = skill };
        var value = known.Base / (double)Tenths;

        if (value < min)
        {
            return false;
        }

        if (value >= max || min >= max)
        {
            return true;
        }

        var chance = (value - min) / (max - min);
        var success = chance >= _random.NextDouble();

        if (_config.GainEnabled && !mobile.IsNpc)
        {
            if (Learns(mobile, known, chance, success))
            {
                Gain(mobile, known);
            }

            // As ModernUO: whether or not the skill itself rose, a success can raise a stat.
            if (success && _skills.Value.TryGetValue(skill, out var content))
            {
                GainStats(mobile, content);
            }
        }

        return success;
    }

    public int Total(MobileEntity mobile)
    {
        return mobile.Skills.Sum(skill => skill.Base);
    }

    // Whether this try teaches: always below ten points; after that the more room is left, and the harder the task,
    // the more often.
    private bool Learns(MobileEntity mobile, MobileSkill known, double chance, bool success)
    {
        if (known.Base < BeginnerBase)
        {
            return true;
        }

        var totalCap = (double)_config.TotalCap * Tenths;
        var gain = (totalCap - Total(mobile)) / totalCap;
        gain += known.Cap > 0 ? (known.Cap - known.Base) / (double)known.Cap : 0;
        gain /= 2;
        gain += (1 - chance) * (success ? SuccessWeight : FailureWeight);
        gain /= 2;
        gain *= _skills.Value.TryGetValue(known.Skill, out var content) ? content.GainFactor : 1.0;

        return Math.Max(gain, LeastGainChance) >= _random.NextDouble();
    }

    private void Gain(MobileEntity mobile, MobileSkill known)
    {
        if (known.Base >= known.Cap || known.Lock != SkillLockType.Up)
        {
            return;
        }

        var amount = known.Base <= BeginnerBase ? _random.Next(4) + 1 : Step;
        var totalCap = _config.TotalCap * Tenths;
        var total = Total(mobile);

        // The fuller the character, the more often something it set to go down gives way.
        if (total / (double)totalCap >= _random.NextDouble() &&
            mobile.Skills.FirstOrDefault(
                other => other.Skill != known.Skill && other.Lock == SkillLockType.Down && other.Base >= amount
            ) is { } lowered)
        {
            _state.SetSkill(mobile, lowered.Skill, lowered.Base - amount);
            total -= amount;
        }

        if (total < totalCap)
        {
            _state.SetSkill(mobile, known.Skill, Math.Min(known.Base + amount, known.Cap));
        }
    }

    private void GainStats(MobileEntity mobile, SkillContent content)
    {
        TryGainStat(mobile, StatType.Str, content.StrGain);
        TryGainStat(mobile, StatType.Dex, content.DexGain);
        TryGainStat(mobile, StatType.Int, content.IntGain);
    }

    // A stat the skill favours, that is up, may rise, by the chance the skill gives it and once in a while.
    private void TryGainStat(MobileEntity mobile, StatType stat, double gain)
    {
        if (gain <= 0 || LockOf(mobile, stat) != StatLockType.Up || gain / StatGainScale <= _random.NextDouble())
        {
            return;
        }

        var now = _time.GetUtcNow();

        if (mobile.StatTriedAt.TryGetValue(stat, out var last) && last.AddMinutes(_config.StatGainMinutes) > now)
        {
            return;
        }

        // The wait starts here, even when nothing rises.
        mobile.StatTriedAt[stat] = now;
        var atrophy = StatTotal(mobile) / (double)_config.StatCap >= _random.NextDouble();
        RaiseStat(mobile, stat, atrophy);
    }

    // Raises the stat by one, as ModernUO's IncreaseStat: when the character is full, or by chance the nearer it is,
    // another stat that is locked down gives a point first.
    private void RaiseStat(MobileEntity mobile, StatType stat, bool atrophy)
    {
        var values = new[] { mobile.Strength, mobile.Dexterity, mobile.Intelligence };
        var before = values.ToArray();
        var others = Enum.GetValues<StatType>().Where(other => other != stat).ToArray();

        if (atrophy || values.Sum() >= _config.StatCap)
        {
            var (first, second) = (others[0], others[1]);

            if (CanLower(mobile, first, values) && (values[(int)first] < values[(int)second] || !CanLower(mobile, second, values)))
            {
                values[(int)first]--;
            }
            else if (CanLower(mobile, second, values))
            {
                values[(int)second]--;
            }
        }

        if (values.Sum() < _config.StatCap && LockOf(mobile, stat) == StatLockType.Up && values[(int)stat] < _config.StatMax)
        {
            values[(int)stat]++;
        }

        if (values.SequenceEqual(before))
        {
            return;
        }

        // The maximum of each bar follows its stat, as at creation.
        _state.SetStats(
            mobile,
            new MobileStatsChange
            {
                Strength = values[0],
                Dexterity = values[1],
                Intelligence = values[2],
                HitsMax = mobile.HitsMax + values[0] - before[0],
                StaminaMax = mobile.StaminaMax + values[1] - before[1],
                ManaMax = mobile.ManaMax + values[2] - before[2]
            }
        );
    }

    private static bool CanLower(MobileEntity mobile, StatType stat, int[] values)
    {
        return LockOf(mobile, stat) == StatLockType.Down && values[(int)stat] > LeastStat;
    }

    private static StatLockType LockOf(MobileEntity mobile, StatType stat)
    {
        return stat switch
        {
            StatType.Str => mobile.StrLock,
            StatType.Dex => mobile.DexLock,
            _            => mobile.IntLock
        };
    }

    private static int StatTotal(MobileEntity mobile)
    {
        return mobile.Strength + mobile.Dexterity + mobile.Intelligence;
    }
}
