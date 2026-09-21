using Moongate.Core.Utils;
using Moongate.Server.Data.Config;
using Moongate.Server.Data.Config.Sections;

namespace Moongate.Tests.Server.Data.Config.Sections;

public sealed class DiagnosticConfigTests
{
    [Fact]
    public void ToOptions_MapsTomlValuesToIndependentOptions()
    {
        var config = TomlUtils.Deserialize<MoongateServerConfig>(
            "[diagnostics]\nenabled = false\ninterval_seconds = 12\nlog_metrics = true\n"
        )!;

        var options = config.Diagnostics.ToOptions();
        config.Diagnostics.Enabled = true;
        config.Diagnostics.IntervalSeconds = 30;
        config.Diagnostics.LogMetrics = false;

        Assert.False(options.Enabled);
        Assert.Equal(TimeSpan.FromSeconds(12), options.Interval);
        Assert.True(options.LogMetrics);
    }

    [Fact]
    public void ToOptions_DefaultsEnableDiagnosticsAtFiveSecondsWithoutMetricLogging()
    {
        var options = new DiagnosticConfig().ToOptions();

        Assert.True(options.Enabled);
        Assert.Equal(TimeSpan.FromSeconds(5), options.Interval);
        Assert.False(options.LogMetrics);
    }

    [Theory]
    [InlineData(true, 0)]
    [InlineData(false, 0)]
    [InlineData(true, -1)]
    [InlineData(false, -1)]
    [InlineData(true, 4_294_968)]
    [InlineData(false, 4_294_968)]
    public void ToOptions_InvalidIntervalRejectsRegardlessOfEnabled(bool enabled, int intervalSeconds)
    {
        var config = new DiagnosticConfig
        {
            Enabled = enabled,
            IntervalSeconds = intervalSeconds
        };

        Assert.Throws<ArgumentOutOfRangeException>(config.ToOptions);
    }

    [Fact]
    public void Validate_NullDiagnosticsSectionRejectsBeforeServerStartup()
    {
        var config = new MoongateServerConfig { Diagnostics = null! };

        Assert.Throws<InvalidOperationException>(config.Validate);
    }
}
