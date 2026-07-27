using System.Net.Sockets;
using DryIoc;
using Moongate.Console.Admin.Plugin.Data.Config;
using Moongate.Console.Admin.Plugin.Services.Hosting;
using Moongate.Server.Services.Commands;
using Moongate.Tests.Support;

namespace Moongate.Tests.Console;

public class ConsoleServerServiceLifecycleTests
{
    private static ConsoleServerService Service(MoongateConsoleConfig config)
    {
        var commands = new CommandService([], new Container(), new StubAccountService());

        return new ConsoleServerService(config, commands, new StubAccountService(), new InlineMainThreadDispatcher());
    }

    [Fact]
    public async Task StartAsync_Disabled_DoesNotBind()
    {
        var service = Service(new() { Enabled = false });

        await service.StartAsync();

        Assert.Equal(0, service.BoundPort);
        await service.StopAsync();
    }

    [Fact]
    public async Task StartAsync_Enabled_BindsAnEphemeralPortThatAcceptsConnections()
    {
        var service = Service(new() { Enabled = true, Address = "127.0.0.1", Port = 0 });

        await service.StartAsync();

        Assert.True(service.BoundPort > 0);

        using (var client = new TcpClient())
        {
            await client.ConnectAsync("127.0.0.1", service.BoundPort);
            Assert.True(client.Connected);
        }

        await service.StopAsync();
    }
}
