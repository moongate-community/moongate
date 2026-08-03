using System.Net.Sockets;
using Moongate.Core.Primitives;
using Moongate.News.Plugin.Entities;
using Moongate.News.Plugin.Services;
using Moongate.News.Plugin.Subscribers;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Data.Session;
using Moongate.Server.Abstractions.Interfaces.Chat;
using Moongate.Tests.Support;
using Moongate.UO.Data.Hues;

namespace Moongate.Tests.News;

/// <summary>
/// What a player is told on the way in. One line, the newest published one: a shard with a long
/// archive must not fire it all at someone walking through the door — that is what <c>.news</c> is
/// for.
/// </summary>
public class NewsMotdSubscriberTests
{
    [Fact]
    public async Task PlayerEnteredWorld_SendsTheNewestPublishedEntry()
    {
        var world = await Fixture.WithAsync(
            ("Older news", true),
            ("Maintenance on Sunday", true)
        );

        await world.EnterAsync();

        Assert.Contains("Maintenance on Sunday", Assert.Single(world.Chat.Sent));
    }

    // A draft is written but not announced. Sending one would publish it by accident, from the one
    // place nobody would think to look.
    [Fact]
    public async Task PlayerEnteredWorld_IgnoresDrafts()
    {
        var world = await Fixture.WithAsync(("Published", true), ("Still a draft", false));

        await world.EnterAsync();

        Assert.Contains("Published", Assert.Single(world.Chat.Sent));
    }

    // A new shard has no news, and greeting someone with an empty line is worse than saying nothing.
    [Fact]
    public async Task PlayerEnteredWorld_WithNothingPublished_SaysNothing()
    {
        var world = await Fixture.WithAsync(("Still a draft", false));

        await world.EnterAsync();

        Assert.Empty(world.Chat.Sent);
    }

    [Fact]
    public async Task PlayerEnteredWorld_WithNoNewsAtAll_SaysNothing()
    {
        var world = await Fixture.WithAsync();

        await world.EnterAsync();

        Assert.Empty(world.Chat.Sent);
    }

    // The event carries a session id, not a session. A session that has already gone -- a client that
    // dropped during the enter-world burst -- must not throw its way out of the subscriber.
    [Fact]
    public async Task PlayerEnteredWorld_ForAnUnknownSession_DoesNotThrow()
    {
        var world = await Fixture.WithAsync(("Maintenance on Sunday", true));

        await world.Subscriber.OnPlayerEnteredWorld(
            new(9999, new(1), new MobileEntity { Id = new(1) }),
            CancellationToken.None
        );

        Assert.Empty(world.Chat.Sent);
    }

    private sealed class Fixture
    {
        private readonly PlayerSession _session;

        private Fixture(NewsMotdSubscriber subscriber, RecordingSessionChat chat, PlayerSession session)
        {
            Subscriber = subscriber;
            Chat = chat;
            _session = session;
        }

        public NewsMotdSubscriber Subscriber { get; }

        public RecordingSessionChat Chat { get; }

        public static async Task<Fixture> WithAsync(params (string Title, bool Published)[] entries)
        {
            var persistence = new FakePersistenceService();
            var news = new NewsService(persistence, new RecordingChatService(), new StubGameLoopContext());

            foreach (var (title, published) in entries)
            {
                var entry = await news.CreateAsync(title, "body", "tom", published);

                // CreateAsync stamps every entry with the same "now" in a test this fast, so the
                // ordering under test would be decided by insertion order rather than by time.
                entry.PublishedAt = DateTime.UtcNow.AddMinutes(Array.IndexOf(entries, (title, published)));
                await persistence.Store<NewsEntity>().UpsertAsync(entry);
            }

            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            var session = new PlayerSession(new(socket, Stream.Null));
            var sessions = new StubSessionManager();

            sessions.Connections.Add(session);

            var chat = new RecordingSessionChat();

            return new(new NewsMotdSubscriber(news, sessions, chat), chat, session);
        }

        public Task EnterAsync()
            => Subscriber.OnPlayerEnteredWorld(
                   new(_session.SessionId, new(1), new MobileEntity { Id = new(1) }),
                   CancellationToken.None
               );
    }

    /// <summary>Records what each session was told, which is the whole of what the subscriber does.</summary>
    private sealed class RecordingSessionChat : IChatService
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
}
