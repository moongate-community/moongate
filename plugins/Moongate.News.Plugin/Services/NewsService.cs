using Moongate.Core.Primitives;
using Moongate.News.Plugin.Interfaces;
using SquidStd.Persistence.Abstractions.Interfaces.Persistence;

namespace Moongate.News.Plugin.Services;

/// <summary>News over the persistence store; the store auto-assigns each entry's <see cref="Serial" /> id.</summary>
public sealed class NewsService : INewsService
{
    private readonly IEntityStore<NewsEntity, Serial> _store;

    public NewsService(IPersistenceService persistence)
    {
        _store = persistence.GetStore<NewsEntity, Serial>();
    }

    public async ValueTask<NewsEntity> CreateAsync(string title, string body, string author, bool isPublished, CancellationToken ct = default)
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

        return news;
    }

    public async ValueTask<NewsEntity?> UpdateAsync(Serial id, string title, string body, bool isPublished, CancellationToken ct = default)
    {
        if (_store.GetById(id) is not { } news)
        {
            return null;
        }

        news.Title = title;
        news.Body = body;
        news.IsPublished = isPublished;
        news.UpdatedAt = DateTime.UtcNow;
        await _store.UpsertAsync(news, ct);

        return news;
    }

    public async ValueTask<bool> DeleteAsync(Serial id, CancellationToken ct = default)
        => await _store.RemoveAsync(id, ct);

    public IReadOnlyList<NewsEntity> GetAll()
        => _store.GetAll().OrderByDescending(news => news.PublishedAt).ToList();

    public IReadOnlyList<NewsEntity> GetPublished()
        => _store.GetAll().Where(news => news.IsPublished).OrderByDescending(news => news.PublishedAt).ToList();

    public NewsEntity? Get(Serial id)
        => _store.GetById(id);
}
