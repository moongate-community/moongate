using Moongate.Server.Commands;
using Moongate.Server.Core.Data.Admin;
using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Types.Commands;
using Moongate.Server.Core.Types.Hosting;
using Moongate.Tests.TestSupport.Admin;

namespace Moongate.Tests.Server.Commands;

public sealed class VersionCommandTests
{
    [Fact]
    public async Task SaysTheVersionTheCodenameHowItWasBuiltAndWhen()
    {
        var context = await RunAsync(Info("Release", new DateTimeOffset(2026, 10, 5, 14, 32, 7, TimeSpan.Zero)));

        var line = Assert.Single(context.Output);
        Assert.Equal(
            (CommandOutputLevel.Information, "Moongate 0.14.0 \"Lilly\" (Release), built 2026-10-05 14:32 UTC."),
            (line.Level, line.Text)
        );
    }

    [Fact]
    public async Task ADebugBuild_SaysSo()
    {
        var context = await RunAsync(Info("Debug", new DateTimeOffset(2026, 10, 5, 14, 32, 7, TimeSpan.Zero)));

        Assert.Contains("(Debug)", Assert.Single(context.Output).Text);
    }

    // Binaries built without the metadata: the line still reads well.
    [Fact]
    public async Task WithoutBuildMetadata_SaysUnknown()
    {
        var context = await RunAsync(Info("", null));

        Assert.Equal("Moongate 0.14.0 \"Lilly\" (unknown), built unknown.", Assert.Single(context.Output).Text);
    }

    [Fact]
    public async Task WithArguments_SaysItsUsage()
    {
        var context = await RunAsync(Info("Release", null), "now");

        var line = Assert.Single(context.Output);
        Assert.Equal((CommandOutputLevel.Error, "Usage: version"), (line.Level, line.Text));
    }

    private static AdminServerInfo Info(string configuration, DateTimeOffset? builtAt)
    {
        return new("0.14.0", "Lilly", ServerMode.Standalone, "test", null, TimeSpan.FromMinutes(5))
        {
            Configuration = configuration, BuiltAt = builtAt
        };
    }

    private static async Task<CommandContext> RunAsync(AdminServerInfo info, params string[] arguments)
    {
        var context = new CommandContext("version", "version", arguments, CommandSourceType.Console, null);

        await new VersionCommand(new SettableServerInfoProvider(info)).ExecuteAsync(context);

        return context;
    }
}
