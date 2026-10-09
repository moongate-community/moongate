using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Types.Commands;
using Moongate.Server.Ultima.Commands;
using Moongate.Tests.Support.Sessions;
using Moongate.Tests.TestSupport.Localization;
using Moongate.Tests.TestSupport.Ultima.Schedule;

namespace Moongate.Tests.Server.Ultima.Commands;

public sealed class EventCommandTests : IAsyncDisposable
{
    private ScheduleServices _services = null!;
    private SessionFixture _fixture = null!;

    [Fact]
    public async Task List_PrintsOneLinePerEvent()
    {
        var context = await RunAsync("list");

        Assert.Equal(
            [
                "halloween: Halloween, 10-20 to 11-02, mode auto, active",
                "winter: Winter, 12-20 to 01-06, mode auto, inactive"
            ],
            context.Output.Select(line => line.Text)
        );
    }

    [Fact]
    public async Task WithoutArguments_IsTheList()
    {
        var context = await RunAsync();

        Assert.Equal(2, context.Output.Count);
    }

    [Theory, InlineData("off", false), InlineData("on", true), InlineData("auto", true)]
    public async Task OnOffAuto_SwitchTheEvent(string mode, bool active)
    {
        var context = await RunAsync(mode, "halloween");

        Assert.Equal($"Event halloween is now {mode}.", Assert.Single(context.Output).Text);
        Assert.Equal(active, _services.Events.IsActive("halloween"));
        Assert.Equal(mode, _services.Events.Get("halloween")!.Mode);
    }

    [Fact]
    public async Task AnUnknownId_ListsTheIds()
    {
        var context = await RunAsync("on", "nothing");

        var line = Assert.Single(context.Output);
        Assert.Equal(CommandOutputLevel.Error, line.Level);
        Assert.Equal("No event is called nothing. Events: halloween, winter", line.Text);
    }

    [Theory, InlineData("on"), InlineData("flip", "halloween"), InlineData("on", "halloween", "extra")]
    public async Task WrongArguments_ShowTheUsage(params string[] arguments)
    {
        var context = await RunAsync(arguments);

        Assert.Equal(
            (CommandOutputLevel.Error, "Usage: event list | on <id> | off <id> | auto <id>"),
            (Assert.Single(context.Output).Level, context.Output[0].Text)
        );
    }

    [Fact]
    public async Task Texts_AreInTheServerLanguage()
    {
        var context = await RunAsync(["on", "halloween"], TestLocalization.With((30227, "Evento {0}: {1}.")));

        Assert.Equal("Evento halloween: on.", Assert.Single(context.Output).Text);
    }

    private Task<CommandContext> RunAsync(params string[] arguments)
    {
        return RunAsync(arguments, null);
    }

    private async Task<CommandContext> RunAsync(
        string[] arguments, Moongate.Server.Core.Interfaces.Services.ILocalizationService? localization
    )
    {
        _services = await ScheduleServices.CreateAsync();
        _fixture = await SessionFixture.CreateAsync();
        var context = new CommandContext("event", "event", arguments, CommandSourceType.Console, null);

        await new EventCommand(_services.Events, _fixture.Loop, localization).ExecuteAsync(context);

        return context;
    }

    public async ValueTask DisposeAsync()
    {
        if (_fixture is not null)
        {
            await _fixture.DisposeAsync();
        }
    }
}
