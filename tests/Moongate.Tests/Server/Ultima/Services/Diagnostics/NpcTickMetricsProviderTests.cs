using Moongate.Core.Primitives;
using Moongate.Server.Core.Types.Diagnostics;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Services.Diagnostics;
using Moongate.Tests.TestSupport.Scripting;

namespace Moongate.Tests.Server.Ultima.Services.Diagnostics;

public sealed class NpcTickMetricsProviderTests
{
    [Fact]
    public async Task Collect_ReportsTheAwakeNpcsAndTheThinks()
    {
        var timers = new RecordingTimerService();
        var ticks = new NpcTickService(timers, new NpcsConfig());
        ticks.Wake(new MobileEntity { Id = new Serial(0x100), Name = "an orc" });
        ticks.Wake(new MobileEntity { Id = new Serial(0x101), Name = "an orc" });
        timers.Fire(timers.Timers[0].Id);
        var provider = new NpcTickMetricsProvider(ticks);

        var samples = await provider.CollectAsync();

        Assert.Equal("npcs", provider.ProviderName);
        var awake = Assert.Single(samples, sample => sample.Name == "awake");
        Assert.Equal(2, awake.Value);
        Assert.Equal(DiagnosticMetricType.Gauge, awake.Type);
        var thinks = Assert.Single(samples, sample => sample.Name == "thinks_total");
        Assert.Equal(1, thinks.Value);
        Assert.Equal(DiagnosticMetricType.Counter, thinks.Type);
    }
}
