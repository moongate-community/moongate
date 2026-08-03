using Moongate.Core.Types;
using Moongate.News.Plugin.Interfaces;
using Moongate.Server.Abstractions.Attributes;
using Moongate.Server.Abstractions.Data.Commands;
using Moongate.Server.Abstractions.Interfaces.Commands;
using Moongate.Server.Abstractions.Types;

namespace Moongate.News.Plugin.Commands;

/// <summary>
/// Prints the shard's published news to whoever ran it.
/// <para>
/// The archive to the MOTD's headline: entering the world shows the newest entry only, and this is
/// where the rest lives. Drafts never appear — a player-facing surface that leaked one would publish
/// it as surely as the greeting would.
/// </para>
/// </summary>
[Command(
    "news",
    AccountLevelType.Player,
    "Shows the shard's published news.",
    Sources = CommandSourceType.InGame | CommandSourceType.Console | CommandSourceType.Rest
)]
public sealed class NewsCommand : ICommand
{
    private readonly INewsService _news;

    public NewsCommand(INewsService news)
    {
        _news = news;
    }

    public void Execute(CommandContext context)
    {
        var published = _news.GetPublished();

        if (published.Count == 0)
        {
            // Silence would read as a broken command rather than as an empty archive.
            context.Reply("There is no news.");

            return;
        }

        foreach (var entry in published)
        {
            context.Reply($"{entry.Title} — {entry.Body}");
        }
    }
}
