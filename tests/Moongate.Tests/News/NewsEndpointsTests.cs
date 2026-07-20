using System.Net;
using System.Net.Http.Json;
using DryIoc;
using Moongate.Core.Types;
using Moongate.News.Plugin.Data.Api;
using Moongate.News.Plugin.Endpoints;
using Moongate.News.Plugin.Interfaces;
using Moongate.News.Plugin.Services;
using Moongate.Tests.Support;
using Xunit;

namespace Moongate.Tests.News;

public class NewsEndpointsTests
{
    // Wires the real news endpoints over real HTTP. TestApiServer already registers a fake
    // IPersistenceService, so NewsService gets an in-memory store.
    private static Task<TestApiServer> StartAsync()
        => TestApiServer.StartAsync(
            AccountLevelType.GrandMaster,
            configure: container =>
            {
                container.Register<INewsService, NewsService>(Reuse.Singleton);
                var news = container.Resolve<INewsService>();
                container.RegisterApiEndpointInstance(new NewsAdminEndpoints(news));
                container.RegisterApiEndpointInstance(new NewsEndpoints(news));
            });

    [Fact]
    public async Task Admin_can_create_then_read_it_back()
    {
        await using var server = await StartAsync();
        await server.AuthenticateAsync();

        var create = await server.Client.PostAsJsonAsync(
            "/api/v1/admin/news", new CreateNewsRequest("Patch 1", "notes", IsPublished: true));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<NewsResponse>();
        Assert.Equal("Patch 1", created!.Title);
        Assert.NotEqual(0u, created.Id);

        var one = await server.Client.GetFromJsonAsync<NewsResponse>($"/api/v1/admin/news/{created.Id}");
        Assert.Equal("Patch 1", one!.Title);
    }

    [Fact]
    public async Task Admin_endpoints_require_auth()
    {
        await using var server = await StartAsync();

        var res = await server.Client.GetAsync("/api/v1/admin/news");

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Update_and_delete_report_404_for_missing()
    {
        await using var server = await StartAsync();
        await server.AuthenticateAsync();

        var put = await server.Client.PutAsJsonAsync(
            "/api/v1/admin/news/999999", new UpdateNewsRequest("x", "y", true));
        Assert.Equal(HttpStatusCode.NotFound, put.StatusCode);

        var del = await server.Client.DeleteAsync("/api/v1/admin/news/999999");
        Assert.Equal(HttpStatusCode.NotFound, del.StatusCode);
    }

    [Fact]
    public async Task Public_list_returns_only_published()
    {
        await using var server = await StartAsync();
        await server.AuthenticateAsync();
        await server.Client.PostAsJsonAsync("/api/v1/admin/news", new CreateNewsRequest("draft", "b", false));
        await server.Client.PostAsJsonAsync("/api/v1/admin/news", new CreateNewsRequest("live", "b", true));

        var list = await server.Client.GetFromJsonAsync<List<NewsResponse>>("/api/v1/news");

        Assert.Equal("live", Assert.Single(list!).Title);
    }

    [Fact]
    public async Task Public_get_hides_drafts_as_404()
    {
        await using var server = await StartAsync();
        await server.AuthenticateAsync();
        var draft = await (await server.Client.PostAsJsonAsync(
            "/api/v1/admin/news", new CreateNewsRequest("draft", "b", false))).Content.ReadFromJsonAsync<NewsResponse>();

        var res = await server.Client.GetAsync($"/api/v1/news/{draft!.Id}");

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }
}
