using System.Net.Sockets;
using Moongate.News.Plugin.Services;
using Moongate.News.Plugin.Subscribers;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Data.Session;
using Moongate.Server.Abstractions.Interfaces.Chat;
using Moongate.Server.Services.World;
using Moongate.Server.Subscribers;
using Moongate.Tests.Support;
using Moongate.UO.Data.Hues;
using SquidStd.Core.Directories;
using SquidStd.Services.Core.Services;

namespace Moongate.Tests.Server.World;

/// <summary>
/// Two subscribers greet a player entering the world — the message of the day and the latest news —
/// and the order they speak in is the order they were subscribed. Nothing in the bus documents that
/// guarantee, so it is pinned here rather than assumed: the shard should introduce itself before it
/// hands out announcements.
/// </summary>
public class LoginGreetingOrderTests : IDisposable
{
    private readonly string _root = TemporaryDirectory.Create("mg-greeting-");

    [Fact]
    public async Task TheMotdIsSaidBeforeTheNews()
    {
        var chat = new RecordingChat();
        var sessions = new StubSessionManager();
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        var session = new PlayerSession(new(socket, Stream.Null));

        sessions.Connections.Add(session);

        File.WriteAllText(Path.Combine(_root, "motd.txt"), "Welcome to Britannia!");

        var persistence = new FakePersistenceService();
        var news = new NewsService(persistence, new RecordingChatService(), new StubGameLoopContext());

        await news.CreateAsync("Maintenance on Sunday", "at 22:00", "tom", true);

        var bus = new EventBusService();

        // Subscribed in the order the application registers them: the server's own subscribers first,
        // then the plugins'.
        new MotdSubscriber(new MotdService(new(_root, []), "0.4.2", () => 1), sessions, chat).Subscribe(bus);
        new NewsMotdSubscriber(news, sessions, chat).Subscribe(bus);

        await bus.PublishAsync(PlayerEnteredWorldEventFor(session.SessionId));

        Assert.Equal(
            ["Moongate v0.4.2", "Total online players: 1", "Welcome to Britannia!", "Maintenance on Sunday"],
            chat.Sent
        );
    }

    private static Moongate.Server.Abstractions.Data.Events.PlayerEnteredWorldEvent PlayerEnteredWorldEventFor(
        long sessionId
    )
        => new(sessionId, new(1), new MobileEntity { Id = new(1) });

    private sealed class RecordingChat : IChatService
    {
        public List<string> Sent { get; } = [];

        public void SendSystemMessage(PlayerSession session, string text, Hue? hue = null)
            => Sent.Add(text);

        public void Broadcast(string text, Hue? hue = null)
            => throw new NotSupportedException();

        public void Say(MobileEntity speaker, Moongate.UO.Data.Types.ChatMessageType type, string text, Hue hue, int range)
            => throw new NotSupportedException();

        public bool SayAs(MobileEntity speaker, string text)
            => throw new NotSupportedException();
    }

    public void Dispose()
        => TemporaryDirectory.Remove(_root);
}
