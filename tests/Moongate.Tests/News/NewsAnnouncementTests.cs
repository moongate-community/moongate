using Moongate.News.Plugin.Services;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Data.Session;
using Moongate.Server.Abstractions.Interfaces.Chat;
using Moongate.Tests.Support;
using Moongate.UO.Data.Hues;

namespace Moongate.Tests.News;

/// <summary>
/// When the shard interrupts everyone. Publishing is the event worth announcing; editing something
/// already published is not, or a typo corrected three times becomes three announcements.
/// </summary>
public class NewsAnnouncementTests
{
    [Fact]
    public async Task CreatingAPublishedEntry_Announces()
    {
        var world = Fixture.Build();

        await world.News.CreateAsync("Maintenance on Sunday", "at 22:00", "tom", true);

        Assert.Contains("Maintenance on Sunday", Assert.Single(world.Chat.Broadcasts));
    }

    [Fact]
    public async Task CreatingADraft_SaysNothing()
    {
        var world = Fixture.Build();

        await world.News.CreateAsync("Still cooking", "soon", "tom", false);

        Assert.Empty(world.Chat.Broadcasts);
    }

    [Fact]
    public async Task PublishingADraft_Announces()
    {
        var world = Fixture.Build();
        var draft = await world.News.CreateAsync("Still cooking", "soon", "tom", false);

        await world.News.UpdateAsync(draft.Id, "Now ready", "here it is", true);

        Assert.Contains("Now ready", Assert.Single(world.Chat.Broadcasts));
    }

    // The whole reason the trigger is the transition rather than the save: fixing a typo must not
    // interrupt everyone again.
    [Fact]
    public async Task EditingAnAlreadyPublishedEntry_SaysNothingAgain()
    {
        var world = Fixture.Build();
        var entry = await world.News.CreateAsync("Maintenance on Sunday", "at 22:00", "tom", true);

        world.Chat.Broadcasts.Clear();

        await world.News.UpdateAsync(entry.Id, "Maintenance on Sunday", "at 22:30", true);

        Assert.Empty(world.Chat.Broadcasts);
    }

    [Fact]
    public async Task UnpublishingAnEntry_SaysNothing()
    {
        var world = Fixture.Build();
        var entry = await world.News.CreateAsync("Maintenance on Sunday", "at 22:00", "tom", true);

        world.Chat.Broadcasts.Clear();

        await world.News.UpdateAsync(entry.Id, "Maintenance on Sunday", "at 22:00", false);

        Assert.Empty(world.Chat.Broadcasts);
    }

    // Publishing again after a retraction is a fresh announcement: the shard said it was off, and is
    // now saying it is back on.
    [Fact]
    public async Task RepublishingAfterARetraction_AnnouncesAgain()
    {
        var world = Fixture.Build();
        var entry = await world.News.CreateAsync("Maintenance on Sunday", "at 22:00", "tom", true);

        await world.News.UpdateAsync(entry.Id, "Maintenance on Sunday", "at 22:00", false);

        world.Chat.Broadcasts.Clear();

        await world.News.UpdateAsync(entry.Id, "Maintenance on Sunday", "at 22:00", true);

        Assert.Single(world.Chat.Broadcasts);
    }

    [Fact]
    public async Task UpdatingSomethingThatDoesNotExist_SaysNothing()
    {
        var world = Fixture.Build();

        Assert.Null(await world.News.UpdateAsync(new(0xDEAD), "Ghost", "boo", true));
        Assert.Empty(world.Chat.Broadcasts);
    }

    // The announcement touches every live session, so it runs where world writes run. Posting it is
    // what the REST console already does rather than writing from the request thread.
    [Fact]
    public async Task TheAnnouncement_RunsOnTheGameLoop()
    {
        var world = Fixture.Build();

        await world.News.CreateAsync("Maintenance on Sunday", "at 22:00", "tom", true);

        Assert.Equal(1, world.Loop.PostCount);
    }

    private sealed class Fixture
    {
        private Fixture(NewsService news, RecordingBroadcastChat chat, StubGameLoopContext loop)
        {
            News = news;
            Chat = chat;
            Loop = loop;
        }

        public NewsService News { get; }

        public RecordingBroadcastChat Chat { get; }

        public StubGameLoopContext Loop { get; }

        public static Fixture Build()
        {
            var chat = new RecordingBroadcastChat();
            var loop = new StubGameLoopContext();

            return new(new NewsService(new FakePersistenceService(), chat, loop), chat, loop);
        }
    }

    private sealed class RecordingBroadcastChat : IChatService
    {
        public List<string> Broadcasts { get; } = [];

        public void Broadcast(string text, Hue? hue = null)
            => Broadcasts.Add(text);

        public void SendSystemMessage(PlayerSession session, string text, Hue? hue = null)
            => throw new NotSupportedException();

        public void Say(MobileEntity speaker, Moongate.UO.Data.Types.ChatMessageType type, string text, Hue hue, int range)
            => throw new NotSupportedException();

        public bool SayAs(MobileEntity speaker, string text)
            => throw new NotSupportedException();
    }
}
