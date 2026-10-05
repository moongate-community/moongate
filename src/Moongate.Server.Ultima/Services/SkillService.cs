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
///     too, less than a success. The value of a skill is its base: stats add nothing to it yet.
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

    private readonly IMobileStateService _state;
    private readonly SkillsConfig _config;
    private readonly Random _random;
    private readonly Lazy<Dictionary<SkillType, SkillContent>> _skills;

    public SkillService(IMobileStateService state, IDataLoaderService data, SkillsConfig config, Random? random = null)
    {
        _state = state;
        _config = config;
        _random = random ?? Random.Shared;
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

        if (_config.GainEnabled && !mobile.IsNpc && Learns(mobile, known, chance, success))
        {
            Gain(mobile, known);
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
}
