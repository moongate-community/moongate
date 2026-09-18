using Moongate.Server.Core.Data.Diagnostics;

namespace Moongate.Tests.Server.Core.Data.Diagnostics;

public sealed class DiagnosticOptionsTests
{
    [Theory, InlineData(1d), InlineData(4_294_967_294d)]
    public void Validate_AcceptsInclusivePeriodicTimerBoundaries(double milliseconds)
    {
        var options = new DiagnosticOptions { Interval = TimeSpan.FromMilliseconds(milliseconds) };

        options.Validate();
    }

    [Theory, InlineData(-1d), InlineData(0d), InlineData(0.9999d), InlineData(4_294_967_295d)]
    public void Validate_RejectsIntervalsOutsidePeriodicTimerBoundaries(double milliseconds)
    {
        var options = new DiagnosticOptions
        {
            Enabled = false,
            Interval = TimeSpan.FromMilliseconds(milliseconds)
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }
}
