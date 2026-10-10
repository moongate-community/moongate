using Moongate.Server.Ultima.Services.Internal;
using Moongate.Server.Ultima.Types.Combat;
using Moongate.Server.Ultima.Types.Items;
using Moongate.Tests.TestSupport.Randomness;

namespace Moongate.Tests.Server.Ultima.Services.Internal;

public sealed class CombatFormulasTests
{
    [Theory]
    [InlineData(100, 30, 1.0, 2.5)]
    [InlineData(100, 35, 1.0, 15000.0 / 7000)]
    [InlineData(50, 30, 1.0, 15000.0 / 4500)]
    [InlineData(100, 30, 2.0, 1.25)]
    [InlineData(100, 30, 0.5, 5.0)]
    public void SwingDelay_IsTheClassicFormulaDividedByTheGlobalSpeed(int stamina, int speed, double global, double expected)
    {
        Assert.Equal(expected, CombatFormulas.SwingDelaySeconds(stamina, speed, global), 6);
    }

    [Fact]
    public void SwingDelay_ASpeedOfNothing_IsAnHour_NotADivisionByZero()
    {
        Assert.Equal(3600, CombatFormulas.SwingDelaySeconds(100, 0, 1.0));
    }

    [Theory]
    [InlineData(0, 0, 0.5)]
    [InlineData(100, 0, 1.5)]
    [InlineData(50, 50, 0.5)]
    [InlineData(0, 100, 50.0 / 300)]
    public void HitChance_IsAttackerOverTwiceDefender_Plus50(double attacker, double defender, double expected)
    {
        Assert.Equal(expected, CombatFormulas.HitChance(attacker, defender), 6);
    }

    [Fact]
    public void HitChance_NegativeSkillsAreKeptAboveTheirFloor()
    {
        Assert.Equal(0.1 / (0.1 * 2), CombatFormulas.HitChance(-100, -100), 6);
    }

    [Theory]
    [InlineData(ItemQualityType.Exceptional, 12)]
    [InlineData(ItemQualityType.Low, 8)]
    [InlineData(ItemQualityType.Regular, 10)]
    public void ScaleDamage_OfAWeaponOfAQuality_IsAFifthHigherOrLower(ItemQualityType quality, int expected)
    {
        Assert.Equal(expected, CombatFormulas.ScaleDamage(10, 50, 0, 0, quality: quality));
    }

    [Theory]
    // an axe with lumberjacking 50: a tenth more
    [InlineData(10, 50, 0, 0, 50, 11)]
    // at 100 a fifth, and a tenth on top
    [InlineData(10, 50, 0, 0, 100, 13)]
    // it adds to strength and anatomy: 100 * (1 + 0.1 + 0.1 + 0.18)
    [InlineData(100, 50, 50, 50, 90, 138)]
    public void ScaleDamage_OfAnAxe_FollowsLumberjackingToo(
        int damage, double tactics, double strength, double anatomy, double lumberjacking, int expected
    )
    {
        Assert.Equal(expected, CombatFormulas.ScaleDamage(damage, tactics, strength, anatomy, lumberjacking));
    }

    [Theory]
    // base 10, tactics 100 (+50%), strength 50 (+10%), anatomy 50 (+10%): 10 * 1.5 * 1.2
    [InlineData(10, 100, 50, 50, 18)]
    // tactics 50 changes nothing, no strength or anatomy
    [InlineData(10, 50, 0, 0, 10)]
    // tactics 0: half; anatomy 100 adds a tenth more
    [InlineData(10, 0, 0, 100, 6)]
    public void ScaleDamage_FollowsTacticsStrengthAndAnatomy(
        int damage, double tactics, double strength, double anatomy, int expected
    )
    {
        Assert.Equal(expected, CombatFormulas.ScaleDamage(damage, tactics, strength, anatomy));
    }

    [Theory]
    [InlineData(0.00, 0.07)]
    [InlineData(0.06, 0.07)]
    [InlineData(0.07, 0.07)]
    [InlineData(0.13, 0.07)]
    [InlineData(0.14, 0.14)]
    [InlineData(0.27, 0.14)]
    [InlineData(0.28, 0.15)]
    [InlineData(0.42, 0.15)]
    [InlineData(0.43, 0.22)]
    [InlineData(0.64, 0.22)]
    [InlineData(0.65, 0.35)]
    [InlineData(0.99, 0.35)]
    public void ArmorShare_IsTheZoneTheRollHits(double roll, double expected)
    {
        Assert.Equal(expected, CombatFormulas.ArmorShare(roll));
    }

    [Fact]
    public void Absorbed_IsBetweenHalfAndAllOfTheShareOfTheArmor()
    {
        // armor 100 on the chest (0.35): from 17 to 35, by the second roll
        Assert.Equal(17, CombatFormulas.Absorbed(100, new ScriptedRandom().Doubles(0.99, 0.0)));
        Assert.Equal(35, CombatFormulas.Absorbed(100, new ScriptedRandom().Doubles(0.99, 0.999)));
    }

    [Theory]
    [InlineData(30, 0.0, 15)]
    [InlineData(30, 0.999, 29)]
    [InlineData(1, 0.999, 0)]
    [InlineData(1, 0.0, 0)]
    [InlineData(22, 0.5, 16)]
    public void AbsorbedByPiece_IsFromHalfOfItsRatingUpToJustUnderIt(int rating, double roll, int expected)
    {
        Assert.Equal(expected, CombatFormulas.AbsorbedByPiece(rating, new ScriptedRandom().Doubles(roll)));
    }

    [Fact]
    public void AbsorbedByPiece_NoRating_AbsorbsNothing()
    {
        Assert.Equal(0, CombatFormulas.AbsorbedByPiece(0, new ScriptedRandom()));
        Assert.Equal(0, CombatFormulas.AbsorbedByPiece(-4, new ScriptedRandom()));
    }

    [Theory]
    [InlineData(0.00, ArmorZoneType.Neck)]
    [InlineData(0.10, ArmorZoneType.Hands)]
    [InlineData(0.20, ArmorZoneType.Arms)]
    [InlineData(0.30, ArmorZoneType.Head)]
    [InlineData(0.50, ArmorZoneType.Legs)]
    [InlineData(0.90, ArmorZoneType.Chest)]
    public void ZoneOf_IsThePartTheRollHits(double roll, ArmorZoneType zone)
    {
        Assert.Equal(zone, CombatFormulas.ZoneOf(roll));
    }

    [Fact]
    public void Absorbed_NoArmor_AbsorbsNothing()
    {
        Assert.Equal(0, CombatFormulas.Absorbed(0, new ScriptedRandom()));
        Assert.Equal(0, CombatFormulas.Absorbed(-5, new ScriptedRandom()));
    }

    [Theory]
    [InlineData(10, false, 1.0, 10)]
    [InlineData(10, true, 1.0, 5)]
    [InlineData(10, true, 2.0, 2)]
    [InlineData(1, true, 1.0, 1)]
    public void Halve_AndTheNpcRate_NeverLeaveLessThanOne(int damage, bool halved, double rate, int expected)
    {
        Assert.Equal(expected, CombatFormulas.Reduce(damage, halved, rate));
    }

    [Fact]
    public void Final_IsAtLeastOne_WhateverTheArmor()
    {
        Assert.Equal(1, CombatFormulas.Final(3, 1000, new ScriptedRandom().Doubles(0.99, 0.5)));
    }

    [Fact]
    public void Final_TakesWhatTheArmorAbsorbed()
    {
        // damage 40, armor 100 on the chest: 17 absorbed at least
        Assert.Equal(23, CombatFormulas.Final(40, 100, new ScriptedRandom().Doubles(0.99, 0.0)));
    }
}
