using System.Net;
using System.Net.Http.Json;
using DryIoc;
using Moongate.Core.Types;
using Moongate.Http.Plugin.Data.Console;
using Moongate.Http.Plugin.Endpoints.Console;
using Moongate.Http.Plugin.Interfaces.Console;
using Moongate.Http.Plugin.Services.Console;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Data.Internal;
using Moongate.Server.Abstractions.Interfaces.Accounts;
using Moongate.Server.Abstractions.Interfaces.Chat;
using Moongate.Server.Abstractions.Types;
using Moongate.Server.Commands;
using Moongate.Server.Services.Commands;
using Moongate.Tests.Support;
using Moongate.UO.Data.Hues;
using Moongate.UO.Data.Types;
using Xunit;

namespace Moongate.Tests.Http.Endpoints.Console;

public class ConsoleEndpointsTests
{
    private sealed class RecordingChat : IChatService
    {
        public void Broadcast(string text, Hue? hue = null)
        {
        }

        public void Say(MobileEntity speaker, ChatMessageType type, string text, Hue hue, int range)
        {
        }
    }

    // Wires the real ConsoleEndpoints (over real HTTP) against the given registry, an inline dispatcher
    // (so a POSTed command runs before the request returns) and a broadcast command opted into Rest.
    private static Task<TestApiServer> StartAsync(ConsoleStreamRegistry registry)
        => TestApiServer.StartAsync(
            AccountLevelType.GrandMaster,
            configure: container =>
            {
                var registration = new CommandRegistration(
                    "broadcast|bc",
                    AccountLevelType.GrandMaster,
                    "Sends a server-wide system message.",
                    CommandSourceType.InGame | CommandSourceType.Console | CommandSourceType.Rest,
                    _ => new BroadcastCommand(new RecordingChat())
                );
                var commands = new CommandService([registration], container, container.Resolve<IAccountService>());

                container.RegisterInstance<IConsoleStreamRegistry>(registry);
                container.RegisterApiEndpointInstance(
                    new ConsoleEndpoints(registry, commands, new InlineMainThreadDispatcher())
                );
            }
        );

    [Fact]
    public async Task Post_with_unknown_connection_returns_404()
    {
        var registry = new ConsoleStreamRegistry();
        await using var server = await StartAsync(registry);
        await server.AuthenticateAsync();

        var response = await server.Client.PostAsJsonAsync(
            "/api/v1/admin/console",
            new { command = "broadcast hi", connectionId = "nope" }
        );

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Post_runs_the_command_and_writes_line_then_done_to_the_connection()
    {
        var registry = new ConsoleStreamRegistry();
        await using var server = await StartAsync(registry);
        await server.AuthenticateAsync();
        var (id, reader) = registry.Open();

        var response = await server.Client.PostAsJsonAsync(
            "/api/v1/admin/console",
            new { command = "broadcast hi", connectionId = id }
        );

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        registry.Close(id); // inline dispatch already ran the command; complete the channel so we can drain it

        var events = new List<ConsoleStreamEvent>();
        await foreach (var evt in reader.ReadAllAsync())
        {
            events.Add(evt);
        }

        Assert.Contains(events, e => e.Event == "line" && e.Text == "Broadcast sent.");
        Assert.Contains(events, e => e.Event == "done" && e.Text == "broadcast hi");
    }
}
