using Moongate.Server.Ultima.Data.Config;

namespace Moongate.Tests.Server.Ultima.Data.Config;

public sealed class HelpConfigTests
{
    [Fact]
    public void Defaults_Are5SecondsAnd10Minutes()
    {
        var config = new HelpConfig();

        Assert.Equal((5, 10), (config.StuckWaitSeconds, config.StuckCooldownMinutes));
        config.Validate();
    }

    [Theory,
     InlineData(0, 10, "ultima.help.stuck_wait_seconds"),
     InlineData(61, 10, "ultima.help.stuck_wait_seconds"),
     InlineData(5, -1, "ultima.help.stuck_cooldown_minutes"),
     InlineData(5, 1441, "ultima.help.stuck_cooldown_minutes")]
    public void Validate_AValueOutOfRange_NamesTheSetting(int wait, int cooldown, string setting)
    {
        var config = new HelpConfig { StuckWaitSeconds = wait, StuckCooldownMinutes = cooldown };

        Assert.Contains(setting, Assert.Throws<InvalidOperationException>(config.Validate).Message);
    }

    [Theory, InlineData(1, 0), InlineData(60, 1440)]
    public void Validate_TheLimits_AreAccepted(int wait, int cooldown)
    {
        new HelpConfig { StuckWaitSeconds = wait, StuckCooldownMinutes = cooldown }.Validate();
    }

    [Fact]
    public void UltimaConfig_HasTheHelpSection_AndValidatesIt()
    {
        var config = new UltimaConfig();

        Assert.Equal(5, config.Help.StuckWaitSeconds);

        config.Help.StuckWaitSeconds = 0;

        Assert.Contains("ultima.help.stuck_wait_seconds", Assert.Throws<InvalidOperationException>(config.Validate).Message);
    }

    [Fact]
    public void Defaults_OfThePages_Are60SecondsAnd30Days()
    {
        var config = new HelpConfig();

        Assert.Equal((60, 30), (config.PageCooldownSeconds, config.PageHistoryDays));
    }

    [Theory,
     InlineData(-1, 30, "ultima.help.page_cooldown_seconds"),
     InlineData(3601, 30, "ultima.help.page_cooldown_seconds"),
     InlineData(60, 0, "ultima.help.page_history_days"),
     InlineData(60, 3651, "ultima.help.page_history_days")]
    public void Validate_APageSettingOutOfRange_NamesIt(int cooldown, int days, string setting)
    {
        var config = new HelpConfig { PageCooldownSeconds = cooldown, PageHistoryDays = days };

        Assert.Contains(setting, Assert.Throws<InvalidOperationException>(config.Validate).Message);
    }

    [Theory, InlineData(0, 1), InlineData(3600, 3650)]
    public void Validate_ThePageLimits_AreAccepted(int cooldown, int days)
    {
        new HelpConfig { PageCooldownSeconds = cooldown, PageHistoryDays = days }.Validate();
    }
}
