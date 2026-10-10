using Moongate.Server.Ultima.Types.Combat;
using Moongate.Server.Ultima.Types.Items;

namespace Moongate.Server.Ultima.Services.Internal;

/// <summary>
///     The numbers of a melee swing, as ModernUO's classic (pre-AOS) combat: how long a swing takes, how likely it is to
///     hit and how much damage it does. Pure: the dice are the caller's.
/// </summary>
internal static class CombatFormulas
{
    // How much a step of quality above or below regular changes the damage of a weapon and the rating of armor.
    public const double QualityDamageStep = 0.2;
    public const int QualityArmorStep = 8;

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
    ///     Raises or lowers the damage by the tactics, the strength and the anatomy of the attacker, and by its
    ///     lumberjacking when it hits with an axe: a fifth more at 100, and a tenth on top. A weapon of exceptional
    ///     quality does a fifth more, one of low quality a fifth less.
    /// </summary>
    public static int ScaleDamage(
        int damage,
        double tactics,
        double strength,
        double anatomy,
        double lumberjacking = 0,
        ItemQualityType quality = ItemQualityType.Regular
    )
    {
        double scaled = damage;
        scaled += scaled * (tactics - Base) / 100;
        var mods = strength / 5 / 100 + anatomy / 5 / 100 + lumberjacking / 5 / 100;

        if (anatomy >= 100)
        {
            mods += 0.1;
        }

        if (lumberjacking >= 100)
        {
            mods += 0.1;
        }

        mods += ((int)quality - (int)ItemQualityType.Regular) * QualityDamageStep;

        scaled += scaled * mods;

        return (int)scaled;
    }

    /// <summary>
    ///     Gets the share of the armor the zone a roll from 0 to 1 hits wears.
    /// </summary>
    public static double ArmorShare(double roll)
    {
        return ShareOf(ZoneOf(roll));
    }

    /// <summary>
    ///     Gets the part of the body a roll from 0 to 1 hits: neck 7%, hands 7%, arms 14%, head 15%, legs 22%, chest 35%.
    /// </summary>
    public static ArmorZoneType ZoneOf(double roll)
    {
        for (var index = 0; index < Zones.Length; index++)
        {
            if (roll < Zones[index].Limit)
            {
                return (ArmorZoneType)index;
            }
        }

        return ArmorZoneType.Chest;
    }

    /// <summary>
    ///     Gets how often a blow lands on the zone, as a share of 1.
    /// </summary>
    public static double ShareOf(ArmorZoneType zone)
    {
        return Zones[(int)zone].Share;
    }

    /// <summary>
    ///     Gets what a worn piece of armor takes off a blow, from half of its armor rating to all of it, as ModernUO's
    ///     <c>BaseArmor.OnHit</c>.
    /// </summary>
    public static int AbsorbedByPiece(int pieceRating, Random random)
    {
        if (pieceRating <= 0)
        {
            return 0;
        }

        // From half of the rating up to just under all of it, as ModernUO: (int)(rating / 2 + rating / 2 * roll).
        return (int)(pieceRating / 2.0 * (1 + random.NextDouble()));
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
