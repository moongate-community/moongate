using Moongate.Server.Ultima.Data.Config;

namespace Moongate.Tests.Server.Ultima.Data.Config;

public sealed class CrimeConfigTests
{
    [Fact]
    public void Defaults_AreTheTwoMinutesOfModernUoAndUox3()
    {
        var config = new CrimeConfig();

        Assert.Equal(120, config.CriminalSeconds);
        config.Validate();
    }

    [Theory, InlineData(0), InlineData(-5), InlineData(86401)]
    public void Validate_ATimeOutOfRange_NamesTheSetting(int seconds)
    {
        var config = new CrimeConfig { CriminalSeconds = seconds };

        Assert.Contains("ultima.crime.criminal_seconds", Assert.Throws<InvalidOperationException>(config.Validate).Message);
    }
}
