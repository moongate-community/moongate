using Moongate.Core.Primitives;
using Moongate.Persistence.Interfaces;

namespace Moongate.News.Plugin.Entities;

/// <summary>A shard news entry, persisted in the "news" store.</summary>
public sealed class NewsEntity : ISerialIdEntity
{
    public Serial Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public string Author { get; set; } = string.Empty;

    public DateTime PublishedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public bool IsPublished { get; set; }
}
