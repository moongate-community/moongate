using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Types.Commands;
using Moongate.Server.Ultima.Commands;
using Moongate.Tests.TestSupport.Localization;
using Moongate.Tests.TestSupport.Ultima.Spawns;

namespace Moongate.Tests.Server.Ultima.Commands;

public sealed class InitialSpawnCommandTests
{
    private readonly StubSpawnRegionService _spawns = new() { Fill = (2778, 21000) };

    [Fact]
    public async Task FillsEveryRegion_AndSaysHowMany()
    {
        var context = new CommandContext("initial_spawn", "initial_spawn", [], CommandSourceType.Console, null);

        await new InitialSpawnCommand(_spawns).ExecuteAsync(context);

        Assert.Equal(1, _spawns.FillAllCalls);
        Assert.Equal(
            "Filling 2778 spawn regions: 21000 to spawn. The spawn messages show the progress.",
            Assert.Single(context.Output).Text
        );
    }

    [Fact]
    public async Task WithArguments_ShowsTheUsage()
    {
        var context = new CommandContext("initial_spawn now", "initial_spawn", ["now"], CommandSourceType.Console, null);

        await new InitialSpawnCommand(_spawns).ExecuteAsync(context);

        Assert.Equal(
            (CommandOutputLevel.Error, "Usage: initial_spawn"),
            (Assert.Single(context.Output).Level, context.Output[0].Text)
        );
        Assert.Equal(0, _spawns.FillAllCalls);
    }

    [Fact]
    public async Task Texts_AreInTheServerLanguage()
    {
        var context = new CommandContext("initial_spawn", "initial_spawn", [], CommandSourceType.Console, null);

        await new InitialSpawnCommand(_spawns, TestLocalization.With((30092, "Riempio {0} regioni: {1} PNG."))).ExecuteAsync(
            context
        );

        Assert.Equal("Riempio 2778 regioni: 21000 PNG.", Assert.Single(context.Output).Text);
    }
}
