using Moongate.Server.Abstractions.Data.Config;
using Moongate.Server.Services.AI;

namespace Moongate.Tests.Server;

public class MoongateConfigTests
{
    [Fact]
    public void Defaults_AreServerReady()
    {
        var config = new MoongateConfig();

        Assert.Equal("0.0.0.0", config.Network.Address);
        Assert.Equal(2593, config.Network.Port);
        Assert.Equal("127.0.0.1", config.Network.PublicAddress);
        Assert.False(string.IsNullOrEmpty(config.ShardName));
    }

    [Fact]
    public void Defaults_IncludeSafeNpcAiLimits()
    {
        var advanced = new MoongateConfig().NpcAi.Advanced;

        Assert.Equal(60, advanced.SectorIdleGraceSeconds);
        Assert.Equal(100, advanced.MinTickMilliseconds);
        Assert.Equal(60_000, advanced.MaxTickMilliseconds);
        Assert.Equal(100, advanced.MaxBrainsPerLoop);
        Assert.Equal(16, advanced.MaxEventsPerBrainWake);
        Assert.Equal(64, advanced.MaxMailboxEvents);
        Assert.Equal(8, advanced.MaxIntentsPerDecision);
        Assert.Equal(50_000, advanced.InstructionBudget);
    }

    [Fact]
    public void Validate_NonPositiveLimit_ThrowsWithPropertyName()
    {
        var config = new NpcAiConfig();
        config.Advanced.MaxBrainsPerLoop = 0;

        var exception = Assert.Throws<InvalidOperationException>(() => NpcAiConfigValidator.Validate(config));

        Assert.Contains(nameof(NpcAiAdvancedConfig.MaxBrainsPerLoop), exception.Message);
    }

    [Fact]
    public void Validate_MinTickExceedsMaxTick_ThrowsWithPropertyName()
    {
        var config = new NpcAiConfig();
        config.Advanced.MinTickMilliseconds = config.Advanced.MaxTickMilliseconds + 1;

        var exception = Assert.Throws<InvalidOperationException>(() => NpcAiConfigValidator.Validate(config));

        Assert.Contains(nameof(NpcAiAdvancedConfig.MinTickMilliseconds), exception.Message);
    }

    [Fact]
    public void Validate_MailboxSmallerThanWakeBudget_ThrowsWithPropertyName()
    {
        var config = new NpcAiConfig();
        config.Advanced.MaxMailboxEvents = config.Advanced.MaxEventsPerBrainWake - 1;

        var exception = Assert.Throws<InvalidOperationException>(() => NpcAiConfigValidator.Validate(config));

        Assert.Contains(nameof(NpcAiAdvancedConfig.MaxMailboxEvents), exception.Message);
    }
}
