using Moongate.Core.Primitives;
using Moongate.News.Plugin.Services;
using Moongate.Tests.Support;
using Xunit;

namespace Moongate.Tests.News;

public class NewsServiceTests
{
    private static NewsService Build()
        => new(new FakePersistenceService());

    [Fact]
    public async Task CreateAsync_assigns_an_id_and_timestamps()
    {
        var before = DateTime.UtcNow;
        var news = await Build().CreateAsync("Patch 1", "notes", "gm", isPublished: true);

        Assert.True(news.Id.IsValid);
        Assert.Equal("gm", news.Author);
        Assert.True(news.PublishedAt >= before);
        Assert.Equal(news.PublishedAt, news.UpdatedAt);
    }

    [Fact]
    public async Task UpdateAsync_touches_UpdatedAt_and_returns_null_for_missing()
    {
        var service = Build();
        var created = await service.CreateAsync("t", "b", "gm", true);

        var updated = await service.UpdateAsync(created.Id, "t2", "b2", false);
        Assert.NotNull(updated);
        Assert.Equal("t2", updated!.Title);
        Assert.False(updated.IsPublished);
        Assert.True(updated.UpdatedAt >= updated.PublishedAt);

        Assert.Null(await service.UpdateAsync(new Serial(999999), "x", "y", true));
    }

    [Fact]
    public async Task GetPublished_excludes_drafts()
    {
        var service = Build();
        await service.CreateAsync("draft", "b", "gm", isPublished: false);
        var pub = await service.CreateAsync("live", "b", "gm", isPublished: true);

        var published = service.GetPublished();
        Assert.Equal(pub.Id, Assert.Single(published).Id);
        Assert.Equal(2, service.GetAll().Count);
    }

    [Fact]
    public async Task DeleteAsync_removes_the_entry()
    {
        var service = Build();
        var created = await service.CreateAsync("t", "b", "gm", true);

        Assert.True(await service.DeleteAsync(created.Id));
        Assert.Null(service.Get(created.Id));
        Assert.False(await service.DeleteAsync(created.Id));
    }
}
