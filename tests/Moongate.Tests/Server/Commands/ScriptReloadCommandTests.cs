using DryIoc;
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

public sealed class ScriptReloadCommandTests
{
    private readonly FakeScriptEngine _engine = new();
    private readonly StubGameLoop _loop = new();

    [Fact]
    public async Task ExecuteAsync_WithoutAFile_PrintsTheUsageLine()
    {
        var service = await CreateStartedServiceAsync();

        var line = Assert.Single(await service.ExecuteAsync("script reload"));

        Assert.Equal("Usage: script reload <file relative to scripts/>", line.Text);
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

    private async Task<CommandSystemService> CreateStartedServiceAsync()
    {
        var container = new Container();
        container.RegisterInstance<IScriptEngine>(_engine);
        container.RegisterInstance<IGameLoopService>(_loop);
        container.RegisterCommand<ScriptReloadCommand>(
            "script",
            "Reloads a script file: script reload <file>.",
            CommandSourceType.Console,
            AccountType.Administrator
        );
        var service = new CommandSystemService(container.Resolve<CommandRegistry>(), container);
        await service.StartAsync();

        return service;
    }
}
