using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Types.Commands;
using Moongate.Server.Ultima.Commands;
using Moongate.Tests.TestSupport.Localization;
using Moongate.Tests.TestSupport.Ultima.World;

namespace Moongate.Tests.Server.Ultima.Commands;

public sealed class GlobalLightCommandTests
{
    private readonly RecordingLightService _light = new();

    [Fact]
    public async Task WithALevel_SetsTheOverride()
    {
        var context = await RunAsync("26");

        Assert.Equal(26, _light.Override);
        Assert.Equal("The global light is now 26.", Assert.Single(context.Output).Text);
    }

    [Fact]
    public async Task WithoutALevel_ClearsTheOverride()
    {
        await _light.SetOverrideAsync(26);

        var context = await RunAsync();

        Assert.Null(_light.Override);
        Assert.Equal("The global light follows the time of day again.", Assert.Single(context.Output).Text);
    }

    [Theory, InlineData("-1"), InlineData("32"), InlineData("dark"), InlineData("1 2")]
    public async Task ABadLevel_ShowsTheUsage_AndChangesNothing(string arguments)
    {
        var context = await RunAsync(arguments.Split(' '));

        Assert.Equal((CommandOutputLevel.Error, "Usage: globallight [0-31]"), (Assert.Single(context.Output).Level, context.Output[0].Text));
        Assert.Equal(0, _light.Calls);
    }

    [Fact]
    public async Task Texts_AreInTheServerLanguage()
    {
        var context = new CommandContext("globallight 3", "globallight", ["3"], CommandSourceType.Console, null);

        await new GlobalLightCommand(_light, TestLocalization.With((30060, "La luce globale ora è {0}."))).ExecuteAsync(context);

        Assert.Equal("La luce globale ora è 3.", Assert.Single(context.Output).Text);
    }

    private async Task<CommandContext> RunAsync(params string[] arguments)
    {
        var context = new CommandContext("globallight", "globallight", arguments, CommandSourceType.Console, null);

        await new GlobalLightCommand(_light).ExecuteAsync(context);

        return context;
    }
}
