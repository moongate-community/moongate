using Moongate.News.Plugin.Entities;

namespace Moongate.News.Plugin.Data.Api;

/// <summary>A news entry as returned by the API.</summary>
public sealed record NewsResponse(
    uint Id,
    string Title,
    string Body,
    string Author,
    DateTime PublishedAt,
    DateTime UpdatedAt,
    bool IsPublished
)
{
    /// <summary>Projects a persisted entry into its API shape.</summary>
    public static NewsResponse From(NewsEntity news)
        => new(news.Id.Value, news.Title, news.Body, news.Author, news.PublishedAt, news.UpdatedAt, news.IsPublished);
}
