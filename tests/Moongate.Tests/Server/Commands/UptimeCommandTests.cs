using Moongate.Server.Commands;
using Moongate.Server.Core.Data.Admin;
using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Types.Commands;
using Moongate.Server.Core.Types.Hosting;
using Moongate.Tests.TestSupport.Admin;
using Moongate.Tests.TestSupport.Timing;

namespace Moongate.Tests.Server.Commands;

public sealed class UptimeCommandTests
{
    private readonly SettableClock _clock = new() { Now = new DateTimeOffset(2026, 10, 5, 14, 32, 40, TimeSpan.Zero) };

    [Fact]
    public async Task SaysHowLongTheServerHasBeenUp_AndSinceWhen()
    {
        var context = await RunAsync(new TimeSpan(2, 4, 13, 9));

        var line = Assert.Single(context.Output);
        Assert.Equal(
            (CommandOutputLevel.Information, "Up for 2d 4h 13m, since 2026-10-03 10:19 UTC."),
            (line.Level, line.Text)
        );
    }

    [Theory,
     // The two largest units below a day; seconds only in the first hour.
     InlineData(0, 4, 13, 9, "4h 13m"),
     InlineData(0, 0, 13, 9, "13m 9s"),
     InlineData(0, 0, 0, 9, "9s"),
     InlineData(0, 0, 0, 0, "0s"),
     InlineData(1, 0, 0, 0, "1d 0h 0m"),
     InlineData(400, 23, 59, 59, "400d 23h 59m")]
    public async Task ShowsTheTimeInItsLargestUnits(int days, int hours, int minutes, int seconds, string expected)
    {
        var context = await RunAsync(new TimeSpan(days, hours, minutes, seconds));

        Assert.StartsWith($"Up for {expected}, since ", Assert.Single(context.Output).Text);
    }

    // A clock set back after the start must not print a negative time.
    [Fact]
    public async Task ANegativeUptime_IsZero()
    {
        var context = await RunAsync(TimeSpan.FromSeconds(-30));

        Assert.StartsWith("Up for 0s, since 2026-10-05 14:32 UTC.", Assert.Single(context.Output).Text);
    }

    [Fact]
    public async Task WithArguments_SaysItsUsage()
    {
        var context = await RunAsync(TimeSpan.FromMinutes(5), "now");

        var line = Assert.Single(context.Output);
        Assert.Equal((CommandOutputLevel.Error, "Usage: uptime"), (line.Level, line.Text));
    }

    private async Task<CommandContext> RunAsync(TimeSpan uptime, params string[] arguments)
    {
        var info = new AdminServerInfo("0.14.0", "Lilly", ServerMode.Standalone, "test", null, uptime);
        var context = new CommandContext("uptime", "uptime", arguments, CommandSourceType.Console, null);

        await new UptimeCommand(new SettableServerInfoProvider(info), _clock).ExecuteAsync(context);

        return context;
    }
}
