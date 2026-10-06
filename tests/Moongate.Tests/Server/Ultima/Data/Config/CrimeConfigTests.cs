using Moongate.Server.Ultima.Data.Config;

namespace Moongate.Tests.Server.Ultima.Data.Config;

public sealed class CrimeConfigTests
{
    [Fact]
    public void Defaults_AreTheTwoMinutesOfModernUoAndUox3()
    {
        var config = new CrimeConfig();

        Assert.Equal(120, config.CriminalSeconds);
        Assert.Equal((true, "guard", 40, "archerguard"), (config.GuardsEnabled, config.GuardTemplate, config.GuardSeconds, config.ArcherGuardTemplate));
        config.Validate();
    }

    [Theory, InlineData(0), InlineData(86401)]
    public void Validate_AGuardTimeOutOfRange_NamesTheSetting(int seconds)
    {
        var config = new CrimeConfig { GuardSeconds = seconds };

        Assert.Contains("ultima.crime.guard_seconds", Assert.Throws<InvalidOperationException>(config.Validate).Message);
    }

    [Theory, InlineData(""), InlineData("  ")]
    public void Validate_NoGuardTemplate_NamesTheSetting(string template)
    {
        var config = new CrimeConfig { GuardTemplate = template };

        Assert.Contains("ultima.crime.guard_template", Assert.Throws<InvalidOperationException>(config.Validate).Message);
    }

    [Theory, InlineData(""), InlineData("  ")]
    public void Validate_NoArcherGuardTemplate_NamesTheSetting(string template)
    {
        var config = new CrimeConfig { ArcherGuardTemplate = template };

        Assert.Contains("ultima.crime.archer_guard_template", Assert.Throws<InvalidOperationException>(config.Validate).Message);
    }

    [Theory, InlineData(0), InlineData(-5), InlineData(86401)]
    public void Validate_ATimeOutOfRange_NamesTheSetting(int seconds)
    {
        var config = new CrimeConfig { CriminalSeconds = seconds };

        Assert.Contains("ultima.crime.criminal_seconds", Assert.Throws<InvalidOperationException>(config.Validate).Message);
    }
}
