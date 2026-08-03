using Moongate.Core.Interfaces;
using Moongate.Core.Primitives;
using Moongate.News.Plugin.Entities;
using Moongate.News.Plugin.Interfaces;
using Moongate.Server.Abstractions.Interfaces.Chat;
using SquidStd.Persistence.Abstractions.Interfaces.Persistence;

namespace Moongate.News.Plugin.Services;

/// <summary>News over the persistence store; the store auto-assigns each entry's <see cref="Serial" /> id.</summary>
public sealed class NewsService : INewsService
{
    private readonly IEntityStore<NewsEntity, Serial> _store;

    private readonly IChatService _chat;
    private readonly IGameLoopContext _loop;

    public NewsService(IPersistenceService persistence, IChatService chat, IGameLoopContext loop)
    {
        _store = persistence.GetStore<NewsEntity, Serial>();
        _chat = chat;
        _loop = loop;
    }

    public async ValueTask<NewsEntity> CreateAsync(
        string title,
        string body,
        string author,
        bool isPublished,
        CancellationToken ct = default
    )
    {
        var now = DateTime.UtcNow;
        var news = new NewsEntity
        {
            Title = title,
            Body = body,
            Author = author,
            IsPublished = isPublished,
            PublishedAt = now,
            UpdatedAt = now
        };
        await _store.UpsertAsync(news, ct);

        Announce(news, wasPublished: false);

        return news;
    }

    public async ValueTask<bool> DeleteAsync(Serial id, CancellationToken ct = default)
        => await _store.RemoveAsync(id, ct);

    public NewsEntity? Get(Serial id)
        => _store.GetById(id);

    public IReadOnlyList<NewsEntity> GetAll()
        => _store.GetAll().OrderByDescending(news => news.PublishedAt).ToList();

    public IReadOnlyList<NewsEntity> GetPublished()
        => _store.GetAll().Where(news => news.IsPublished).OrderByDescending(news => news.PublishedAt).ToList();

    public async ValueTask<NewsEntity?> UpdateAsync(
        Serial id,
        string title,
        string body,
        bool isPublished,
        CancellationToken ct = default
    )
    {
        if (_store.GetById(id) is not { } news)
        {
            return null;
        }

        // Read before the mutation: whether this save PUBLISHES the entry is the difference between
        // the old state and the new one, and after the assignment the old one is gone.
        var wasPublished = news.IsPublished;

        news.Title = title;
        news.Body = body;
        news.IsPublished = isPublished;
        news.UpdatedAt = DateTime.UtcNow;
        await _store.UpsertAsync(news, ct);

        Announce(news, wasPublished);

        return news;
    }

    /// <summary>
    /// Tells everyone in the world, but only when the entry has just become public. Editing something
    /// already published is not news: a typo corrected three times would be three announcements.
    ///
    /// Posted to the game loop rather than broadcast from here — this reaches every live session, and
    /// the calls that touch sessions belong on the loop. It is what the REST console already does.
    /// </summary>
    private void Announce(NewsEntity news, bool wasPublished)
    {
        if (!news.IsPublished || wasPublished)
        {
            return;
        }

        _loop.Post(() => _chat.Broadcast(news.Title));
    }
}
