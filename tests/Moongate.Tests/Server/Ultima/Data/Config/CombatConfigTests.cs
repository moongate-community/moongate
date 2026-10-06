using Moongate.Server.Ultima.Data.Config;

namespace Moongate.Tests.Server.Ultima.Data.Config;

public sealed class CombatConfigTests
{
    [Fact]
    public void Defaults_AreModernUOsClassicOnes()
    {
        var config = new CombatConfig();

        Assert.Equal((1.0, 0, 1.0, 1, 60, true), (config.GlobalAttackSpeed, config.AttackStamina, config.NpcDamageRate, config.MaxRange, config.CombatantSeconds, config.DisplayDamageNumbers));
        config.Validate();
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
            GlobalAttackSpeed = speed, AttackStamina = stamina, NpcDamageRate = rate, MaxRange = range, CombatantSeconds = seconds
        };

        var error = Assert.Throws<InvalidOperationException>(config.Validate);

        Assert.Contains("ultima.combat.", error.Message);
    }
}
