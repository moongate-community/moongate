using Moongate.Server.Commands;
using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Commands;
using Moongate.Tests.TestSupport.Console;
using Moongate.Tests.TestSupport.Localization;

namespace Moongate.Tests.Server.Commands;

public sealed class ConsoleCommandTests
{
    private readonly RecordingPromptService _prompt = new();

    [Theory, InlineData(new string[0], new[] { "lock" }), InlineData(new[] { "lock" }, new string[0])]
    public void GetArgumentCompletions_OffersLockFirst(string[] previous, string[] expected)
    {
        Assert.Equal(expected, new ConsoleCommand(_prompt).GetArgumentCompletions(previous));
    }

    [Fact]
    public async Task Lock_LocksTheConsoleInput_AndSaysHowToUnlockIt()
    {
        var context = await RunAsync(["lock"]);

        Assert.Equal(["lock"], _prompt.Calls);
        Assert.Equal("Console locked. Press '*' to unlock.", Assert.Single(context.Output).Text);
    }

    [Theory, InlineData(), InlineData("unlock"), InlineData("lock", "now")]
    public async Task AnythingElse_ShowsTheUsage_AndLocksNothing(params string[] arguments)
    {
        var context = await RunAsync(arguments);

        Assert.Empty(_prompt.Calls);
        Assert.Equal(
            (CommandOutputLevel.Error, "Usage: console lock"),
            (Assert.Single(context.Output).Level, context.Output[0].Text)
        );
    }

    [Fact]
    public async Task Texts_AreInTheServerLanguage()
    {
        var context = await RunAsync(["lock"], TestLocalization.With((30102, "Console bloccata. Premi '{0}' per sbloccarla.")));

        Assert.Equal("Console bloccata. Premi '*' per sbloccarla.", Assert.Single(context.Output).Text);
    }

    private async Task<CommandContext> RunAsync(string[] arguments, ILocalizationService? localization = null)
    {
        var context = new CommandContext("console " + string.Join(' ', arguments), "console", arguments, CommandSourceType.Console, null);

        await new ConsoleCommand(_prompt, localization).ExecuteAsync(context);

        return context;
    }
}
