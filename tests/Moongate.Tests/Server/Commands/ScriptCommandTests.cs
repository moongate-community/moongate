using DryIoc;
using Moongate.Scripting.Data.Scripts;
using Moongate.Scripting.Interfaces;
using Moongate.Server.Commands;
using Moongate.Server.Core.Commands;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Core.Types.Commands;
using Moongate.Server.Services.Commands;
using Moongate.Tests.TestSupport.Scripting;

namespace Moongate.Tests.Server.Commands;

public sealed class ScriptCommandTests
{
    private readonly FakeScriptEngine _engine = new();
    private readonly StubGameLoop _loop = new();

    [Theory, InlineData("script"), InlineData("script reload"), InlineData("script metrics extra"), InlineData("script frobnicate")]
    public async Task ExecuteAsync_WithoutAKnownSubcommand_PrintsTheUsageLine(string input)
    {
        var service = await CreateStartedServiceAsync();

        var line = Assert.Single(await service.ExecuteAsync(input));

        Assert.Equal("Usage: script reload <file relative to scripts/> | script metrics", line.Text);
        Assert.Equal(CommandOutputLevel.Error, line.Level);
        Assert.Empty(_engine.Loaded);
        await service.StopAsync();
    }

    [Fact]
    public async Task ExecuteAsync_ReloadsTheFileAndReportsIt()
    {
        var service = await CreateStartedServiceAsync();

        var line = Assert.Single(await service.ExecuteAsync("script reload ai/guard.lua"));

        Assert.Equal("Reloaded ai/guard.lua", line.Text);
        Assert.Equal(CommandOutputLevel.Information, line.Level);
        Assert.Equal(["ai/guard.lua"], _engine.Invalidated);
        Assert.Equal(["ai/guard.lua"], _engine.Loaded);
        await service.StopAsync();
    }

    [Fact]
    public async Task ExecuteAsync_ScriptError_ReportsTheFailureInsteadOfThrowing()
    {
        _engine.LoadFileThrows = new InvalidOperationException("ai/guard.lua:2: attempt to index a nil value");
        var service = await CreateStartedServiceAsync();

        var line = Assert.Single(await service.ExecuteAsync("script reload ai/guard.lua"));

        Assert.Equal("Reload failed: ai/guard.lua:2: attempt to index a nil value", line.Text);
        Assert.Equal(CommandOutputLevel.Error, line.Level);
        await service.StopAsync();
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheEngineThrowsSomethingElse_StillCompletesTheOutcome()
    {
        _engine.LoadFileThrows = new IOException("the file is locked");
        var service = await CreateStartedServiceAsync();

        var line = Assert.Single(await service.ExecuteAsync("script reload ai/guard.lua"));

        Assert.Equal("Reload failed: the file is locked", line.Text);
        Assert.Equal(CommandOutputLevel.Error, line.Level);
        await service.StopAsync();
    }

    [Fact]
    public async Task ExecuteAsync_Metrics_PrintsOneLinePerCounter()
    {
        _engine.Metrics = new ScriptExecutionMetrics(3, 12, 40, 7, 2, 1, 5, 4);
        var service = await CreateStartedServiceAsync();

        var lines = await service.ExecuteAsync("script metrics");

        Assert.Equal(
            [
                "Files loaded: 3",
                "Calls started: 12",
                "Coroutines resumed: 40",
                "Coroutines finished: 7",
                "Coroutine errors: 2",
                "Budget aborts: 1",
                "Active coroutines: 5",
                "Memory cap hits: 4"
            ],
            lines.Select(line => line.Text).ToArray()
        );
        Assert.All(lines, line => Assert.Equal(CommandOutputLevel.Information, line.Level));
        Assert.Equal(0, _loop.PostedWorkItems);
        await service.StopAsync();
    }

    private async Task<CommandSystemService> CreateStartedServiceAsync()
    {
        var container = new Container();
        container.RegisterInstance<IScriptEngine>(_engine);
        container.RegisterInstance<IGameLoopService>(_loop);
        container.RegisterCommand<ScriptCommand>(
            "script",
            "Reloads a script file or prints the engine's counters: script reload <file> | script metrics.",
            CommandSourceType.Console,
            AccountType.Administrator
        );
        var service = new CommandSystemService(container.Resolve<CommandRegistry>(), container);
        await service.StartAsync();

        return service;
    }
}
