using Moongate.Server.Ultima.Data.Config;

namespace Moongate.Tests.Server.Ultima.Data.Config;

public sealed class CombatConfigTests
{
    [Fact]
    public void Defaults_AreModernUOsClassicOnes()
    {
        var config = new CombatConfig();

        Assert.Equal(
            (1.0, 0, 1.0, 1, 60, true, 1.0, true, 2, 5),
            (config.GlobalAttackSpeed, config.AttackStamina, config.NpcDamageRate, config.MaxRange, config.CombatantSeconds,
                config.DisplayDamageNumbers, config.ArcheryStandStillSeconds, config.BloodEnabled, config.BloodPieces,
                config.BloodSeconds)
        );
        config.Validate();
    }

    [Theory]
    [InlineData(-0.5)]
    [InlineData(61.0)]
    [InlineData(double.NaN)]
    public void Validate_ABadStandStillTime_Throws(double seconds)
    {
        var config = new CombatConfig { ArcheryStandStillSeconds = seconds };

        var error = Assert.Throws<InvalidOperationException>(config.Validate);

        Assert.Contains("ultima.combat.archery_stand_still_seconds", error.Message);
    }

    [Theory]
    [InlineData(-1, 5, "ultima.combat.blood_pieces")]
    [InlineData(9, 5, "ultima.combat.blood_pieces")]
    [InlineData(2, 0, "ultima.combat.blood_seconds")]
    [InlineData(2, 61, "ultima.combat.blood_seconds")]
    public void Validate_ABadBloodSetting_Throws(int pieces, int seconds, string key)
    {
        var config = new CombatConfig { BloodPieces = pieces, BloodSeconds = seconds };

        var error = Assert.Throws<InvalidOperationException>(config.Validate);

        Assert.Contains(key, error.Message);
    }

    [Fact]
    public void Validate_NoBloodAround_IsAccepted()
    {
        new CombatConfig { BloodPieces = 0 }.Validate();
    }

    [Fact]
    public void Validate_NoStandStillTimeAtAll_IsAccepted()
    {
        new CombatConfig { ArcheryStandStillSeconds = 0 }.Validate();
    }

    [Theory]
    [InlineData(0.0, 0, 1.0, 1, 60)]
    [InlineData(101.0, 0, 1.0, 1, 60)]
    [InlineData(1.0, -1, 1.0, 1, 60)]
    [InlineData(1.0, 101, 1.0, 1, 60)]
    [InlineData(1.0, 0, 0.0, 1, 60)]
    [InlineData(1.0, 0, 101.0, 1, 60)]
    [InlineData(1.0, 0, 1.0, 0, 60)]
    [InlineData(1.0, 0, 1.0, 25, 60)]
    [InlineData(1.0, 0, 1.0, 1, 0)]
    [InlineData(1.0, 0, 1.0, 1, 3601)]
    [InlineData(double.NaN, 0, 1.0, 1, 60)]
    public void Validate_ABadNumber_Throws(double speed, int stamina, double rate, int range, int seconds)
    {
        var config = new CombatConfig
        {
            GlobalAttackSpeed = speed, AttackStamina = stamina, NpcDamageRate = rate, MaxRange = range,
            CombatantSeconds = seconds
        };

        var error = Assert.Throws<InvalidOperationException>(config.Validate);

        Assert.Contains("ultima.combat.", error.Message);
    }
}
