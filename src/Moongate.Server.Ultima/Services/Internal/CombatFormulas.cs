namespace Moongate.Server.Ultima.Services.Internal;

/// <summary>
///     The numbers of a melee swing, as ModernUO's classic (pre-AOS) combat: how long a swing takes, how likely it is to
///     hit and how much damage it does. Pure: the dice are the caller's.
/// </summary>
internal static class CombatFormulas
{
    /// <summary>
    ///     The delay of a swing is an hour for a weapon with no speed, rather than a division by zero, as ModernUO.
    /// </summary>
    public const double NoSpeedDelaySeconds = 3600;

    /// <summary>
    ///     The least damage of a player's fists, as ModernUO's.
    /// </summary>
    public const int FistsMinimumDamage = 1;

    /// <summary>
    ///     The most damage of a player's fists.
    /// </summary>
    public const int FistsMaximumDamage = 8;

    // The skills of the hit chance never count for less than this.
    private const double SkillFloor = -49.9;
    private const double SwingScale = 15000.0;
    private const double Base = 50;

    // The armor of one zone is a share of the whole, as Sphere's body parts: neck, hands, arms, head, legs, chest.
    private static readonly (double Limit, double Share)[] Zones =
    [
        (0.07, 0.07), (0.14, 0.07), (0.28, 0.14), (0.43, 0.15), (0.65, 0.22), (1.0, 0.35)
    ];

    /// <summary>
    ///     Gets the seconds between two swings, <c>15000 / ((stamina + 100) * speed)</c>, divided by the global speed.
    /// </summary>
    public static double SwingDelaySeconds(int stamina, int speed, double globalSpeed)
    {
        if (speed <= 0)
        {
            return NoSpeedDelaySeconds;
        }

        return SwingScale / ((stamina + 100.0) * speed) / globalSpeed;
    }

    /// <summary>
    ///     Gets the chance, from 0 up, that a swing hits: the attacker's weapon skill over twice the defender's, both
    ///     plus 50, as ModernUO. A value of 1 or more always hits.
    /// </summary>
    public static double HitChance(double attackerSkill, double defenderSkill)
    {
        var attack = Math.Max(attackerSkill, SkillFloor) + Base;
        var defense = Math.Max(defenderSkill, SkillFloor) + Base;

        return attack / (defense * 2);
    }

    /// <summary>
    ///     Raises or lowers the damage by the tactics, the strength and the anatomy of the attacker.
    /// </summary>
    public static int ScaleDamage(int damage, double tactics, double strength, double anatomy)
    {
        double scaled = damage;
        scaled += scaled * (tactics - Base) / 100;
        var mods = strength / 5 / 100 + anatomy / 5 / 100;

        if (anatomy >= 100)
        {
            mods += 0.1;
        }

        scaled += scaled * mods;

        return (int)scaled;
    }

    /// <summary>
    ///     Gets the share of the armor the zone a roll from 0 to 1 hits wears.
    /// </summary>
    public static double ArmorShare(double roll)
    {
        foreach (var (limit, share) in Zones)
        {
            if (roll < limit)
            {
                return share;
            }
        }

        return Zones[^1].Share;
    }

    /// <summary>
    ///     Gets the damage one zone of the armor takes off, from half of its share of the armor rating up to all of it.
    /// </summary>
    public static int Absorbed(int armor, Random random)
    {
        if (armor <= 0)
        {
            return 0;
        }

        var share = (int)(armor * ArmorShare(random.NextDouble()));
        var low = share / 2;

        return low + (int)((share - low + 1) * random.NextDouble());
    }

    /// <summary>
    ///     Halves the damage of a swing at a player or by an NPC, and divides it by the rate of the NPCs; never below 1.
    /// </summary>
    public static int Reduce(int damage, bool halved, double npcRate)
    {
        var reduced = halved ? damage / 2 : damage;
        reduced = (int)(reduced / npcRate);

        return Math.Max(reduced, 1);
    }

    /// <summary>
    ///     Takes what the armor absorbed off the damage, leaving at least 1.
    /// </summary>
    public static int Final(int damage, int armor, Random random)
    {
        return Math.Max(damage - Absorbed(armor, random), 1);
    }
}
