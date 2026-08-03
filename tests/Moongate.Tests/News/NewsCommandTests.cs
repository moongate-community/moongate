using Moongate.News.Plugin.Commands;
using Moongate.News.Plugin.Entities;
using Moongate.News.Plugin.Services;
using Moongate.Server.Abstractions.Data.Commands;
using Moongate.Server.Abstractions.Types;
using Moongate.Tests.Support;

namespace Moongate.Tests.News;

/// <summary>
/// Where the archive lives. The MOTD shows one entry on the way in; this is the command that shows
/// the rest, so its job is to print every published entry and nothing else.
/// </summary>
public class NewsCommandTests
{
    [Fact]
    public async Task Execute_PrintsEveryPublishedEntry()
    {
        var replies = await RunAsync(("Maintenance on Sunday", true), ("New zone: the Catacombs", true));

        Assert.Contains(replies, reply => reply.Contains("Maintenance on Sunday", StringComparison.Ordinal));
        Assert.Contains(replies, reply => reply.Contains("New zone: the Catacombs", StringComparison.Ordinal));
    }

    // A draft is written and not announced. The command is a player-facing surface, so a draft
    // leaking here would publish it as surely as the MOTD would.
    [Fact]
    public async Task Execute_LeavesDraftsOut()
    {
        var replies = await RunAsync(("Published", true), ("Still a draft", false));

        Assert.DoesNotContain(replies, reply => reply.Contains("Still a draft", StringComparison.Ordinal));
    }

    // A new shard has published nothing, and a command that answers with silence looks broken.
    [Fact]
    public async Task Execute_WithNothingPublished_SaysSo()
    {
        var replies = await RunAsync(("Still a draft", false));

        Assert.Contains(replies, reply => reply.Contains("no news", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Execute_WithNoNewsAtAll_SaysSo()
    {
        var replies = await RunAsync();

        Assert.Contains(replies, reply => reply.Contains("no news", StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<List<string>> RunAsync(params (string Title, bool Published)[] entries)
    {
        var persistence = new FakePersistenceService();
        var news = new NewsService(persistence, new RecordingChatService(), new StubGameLoopContext());

        foreach (var (title, published) in entries)
        {
            await news.CreateAsync(title, "body", "tom", published);
        }

        var replies = new List<string>();

        new NewsCommand(news).Execute(new(CommandSourceType.InGame, null, [], replies.Add));

        return replies;
    }
}
