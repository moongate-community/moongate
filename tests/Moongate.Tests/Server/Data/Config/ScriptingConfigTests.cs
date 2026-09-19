using Moongate.Server.Data.Config.Sections;

namespace Moongate.Tests.Server.Data.Config;

public sealed class ScriptingConfigTests
{
    [Fact]
    public void ToOptions_CopiesEveryFieldAndTheDirectory()
    {
        var config = new ScriptingConfig
        {
            BootstrapFile = "boot.lua",
            MaxInstructionsPerResume = 20_000,
            HookInterval = 500,
            WriteDefinitions = false
        };

        var options = config.ToOptions("/srv/moongate/scripts");

        Assert.Equal("/srv/moongate/scripts", options.ScriptsDirectory);
        Assert.Equal("boot.lua", options.BootstrapFile);
        Assert.Equal(20_000, options.MaxInstructionsPerResume);
        Assert.Equal(500, options.HookInterval);
        Assert.False(options.WriteDefinitions);
    }

    [Fact]
    public void Defaults_MatchTheDesign()
    {
        var options = new ScriptingConfig().ToOptions("scripts");

        Assert.Equal("init.lua", options.BootstrapFile);
        Assert.Equal(150_000, options.MaxInstructionsPerResume);
        Assert.Equal(1_000, options.HookInterval);
        Assert.True(options.WriteDefinitions);
    }

    [Theory, InlineData(0, 1000), InlineData(1000, 0), InlineData(500, 1000)]
    public void Validate_RejectsANonPositiveOrInvertedBudget(int max, int interval)
    {
        var config = new ScriptingConfig { MaxInstructionsPerResume = max, HookInterval = interval };

        Assert.ThrowsAny<ArgumentException>(() => config.ToOptions("scripts"));
    }
}
