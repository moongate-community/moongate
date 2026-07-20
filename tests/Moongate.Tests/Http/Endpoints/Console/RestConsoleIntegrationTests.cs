using System.Net;
using System.Net.Http.Json;
using System.Net.ServerSentEvents;
using DryIoc;
using Moongate.Core.Types;
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

public class RestConsoleIntegrationTests
{
    private sealed class RecordingChat : IChatService
    {
        public List<string> Broadcasts { get; } = [];

        public void Broadcast(string text, Hue? hue = null)
            => Broadcasts.Add(text);

        public void Say(MobileEntity speaker, ChatMessageType type, string text, Hue hue, int range) { }
    }

    [Fact]
    public async Task Open_stream_then_post_command_streams_reply_over_http()
    {
        var chat = new RecordingChat();
        var registry = new ConsoleStreamRegistry();
        await using var server = await TestApiServer.StartAsync(
            AccountLevelType.GrandMaster,
            configure: container =>
            {
                var registration = new CommandRegistration(
                    "broadcast|bc",
                    AccountLevelType.GrandMaster,
                    "Sends a server-wide system message.",
                    CommandSourceType.InGame | CommandSourceType.Console | CommandSourceType.Rest,
                    _ => new BroadcastCommand(chat));
                var commands = new CommandService([registration], container, container.Resolve<IAccountService>());

                container.RegisterInstance<IConsoleStreamRegistry>(registry);
                container.RegisterApiEndpointInstance(
                    new ConsoleEndpoints(registry, commands, new InlineMainThreadDispatcher()));
            });
        await server.AuthenticateAsync();

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        using var streamResponse = await server.Client.GetAsync(
            "/api/v1/admin/console/stream", HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        Assert.Equal("text/event-stream", streamResponse.Content.Headers.ContentType?.MediaType);

        await using var body = await streamResponse.Content.ReadAsStreamAsync(timeout.Token);
        await using var events = SseParser.Create(body).EnumerateAsync(timeout.Token).GetAsyncEnumerator();

        Assert.True(await events.MoveNextAsync());
        Assert.Equal("ready", events.Current.EventType);
        var connectionId = events.Current.Data;

        var post = await server.Client.PostAsJsonAsync(
            "/api/v1/admin/console",
            new { command = "broadcast hello world", connectionId },
            timeout.Token);
        Assert.Equal(HttpStatusCode.Accepted, post.StatusCode);

        Assert.True(await events.MoveNextAsync());
        Assert.Equal(("line", "Broadcast sent."), (events.Current.EventType, events.Current.Data));
        Assert.True(await events.MoveNextAsync());
        Assert.Equal(("done", "broadcast hello world"), (events.Current.EventType, events.Current.Data));
        Assert.Contains("hello world", chat.Broadcasts);
    }
}
