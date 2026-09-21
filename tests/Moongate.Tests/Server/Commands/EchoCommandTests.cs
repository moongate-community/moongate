using DryIoc;
using Moongate.Server.Commands;
using Moongate.Server.Core.Commands;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Core.Types.Commands;
using Moongate.Server.Services.Commands;

namespace Moongate.Tests.Server.Commands;

public sealed class EchoCommandTests
{
    [Fact]
    public async Task ExecuteAsync_IsReachableThroughItsAlias()
    {
        var service = await CreateStartedServiceAsync();

        var line = Assert.Single(await service.ExecuteAsync("e alias works"));

        Assert.Equal("alias works", line.Text);
        await service.StopAsync();
    }

    [Fact]
    public async Task ExecuteAsync_JoinsArgumentsWithSingleSpaces()
    {
        var service = await CreateStartedServiceAsync();

        var line = Assert.Single(await service.ExecuteAsync("echo  hello   world "));

        Assert.Equal("hello world", line.Text);
        Assert.Equal(CommandOutputLevel.Information, line.Level);
        await service.StopAsync();
    }

    [Fact]
    public async Task ExecuteAsync_WithoutArgumentsProducesOneEmptyLine()
    {
        var service = await CreateStartedServiceAsync();

        var line = Assert.Single(await service.ExecuteAsync("echo"));

        Assert.Equal("", line.Text);
        await service.StopAsync();
    }

    private static async Task<CommandSystemService> CreateStartedServiceAsync()
    {
        var container = new Container();
        container.RegisterCommand<EchoCommand>(
            "echo|e",
            "Echoes back its arguments.",
            CommandSourceType.Console | CommandSourceType.InGame,
            AccountType.Regular
        );
        var service = new CommandSystemService(container.Resolve<CommandRegistry>(), container);
        await service.StartAsync();

        return service;
    }
}
