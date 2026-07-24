using Moongate.Core.Primitives;
using Moongate.News.Plugin.Entities;

namespace Moongate.News.Plugin.Interfaces;

/// <summary>Creates, updates, deletes and reads shard news.</summary>
public interface INewsService
{
    /// <summary>Creates a news entry; the store assigns its id. Timestamps are set to now.</summary>
    ValueTask<NewsEntity> CreateAsync(
        string title, string body, string author, bool isPublished, CancellationToken ct = default
    );

    /// <summary>Updates an entry's title/body/published state and its <c>UpdatedAt</c>; null if not found.</summary>
    ValueTask<NewsEntity?> UpdateAsync(
        Serial id, string title, string body, bool isPublished, CancellationToken ct = default
    );

    /// <summary>Deletes an entry; false if it did not exist.</summary>
    ValueTask<bool> DeleteAsync(Serial id, CancellationToken ct = default);

    /// <summary>Every entry, drafts included, newest first.</summary>
    IReadOnlyList<NewsEntity> GetAll();

    /// <summary>Published entries only, newest first.</summary>
    IReadOnlyList<NewsEntity> GetPublished();

    /// <summary>One entry by id, or null.</summary>
    NewsEntity? Get(Serial id);
}
