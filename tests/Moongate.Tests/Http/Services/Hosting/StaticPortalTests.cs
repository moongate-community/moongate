using System.Net;
using Moongate.Tests.Support;

namespace Moongate.Tests.Http.Services.Hosting;

public sealed class StaticPortalTests : IAsyncDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "mg-portal-" + Guid.NewGuid().ToString("N"));

    public StaticPortalTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "assets"));
        File.WriteAllText(Path.Combine(_root, "index.html"), "<!doctype html><title>portal</title>PORTAL_INDEX");
        File.WriteAllText(Path.Combine(_root, "assets", "app-abc123.js"), "export default 1");
    }

    [Fact]
    public async Task Root_ServesThePortalIndex()
    {
        await using var server = await TestApiServer.StartAsync(uiDistPath: _root);

        var response = await server.Client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("PORTAL_INDEX", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task DeepLink_ServesTheIndexSoTheRouterCanHandleIt()
    {
        await using var server = await TestApiServer.StartAsync(uiDistPath: _root);

        var response = await server.Client.GetAsync("/characters/42");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("PORTAL_INDEX", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task HashedAsset_IsServedAndCachedForever()
    {
        await using var server = await TestApiServer.StartAsync(uiDistPath: _root);

        var response = await server.Client.GetAsync("/assets/app-abc123.js");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Vite fingerprints the name, so the bytes behind it never change; index.html carries the pointers
        // and must not be cached, or a deploy would keep serving the previous bundle's names.
        Assert.Equal("public, max-age=31536000, immutable", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task Index_IsNotCached()
    {
        await using var server = await TestApiServer.StartAsync(uiDistPath: _root);

        var response = await server.Client.GetAsync("/");

        Assert.Equal("no-cache", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task UnmatchedApiRoute_Is404Json_NotThePortal()
    {
        // The whole point of constraining the fallback. Without it a REST client asking for a route that
        // does not exist would get an HTML page with status 200 and no way to tell it went wrong.
        await using var server = await TestApiServer.StartAsync(uiDistPath: _root);

        var response = await server.Client.GetAsync("/api/v1/does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.DoesNotContain("PORTAL_INDEX", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnmatchedNonGet_Is404_NotThePortal()
    {
        // A POST to a route that does not exist is a client error, not a page view.
        await using var server = await TestApiServer.StartAsync(uiDistPath: _root);

        var response = await server.Client.PostAsync("/whatever", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.DoesNotContain("PORTAL_INDEX", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExistingApiRoute_StillAnswers()
    {
        await using var server = await TestApiServer.StartAsync(uiDistPath: _root);

        Assert.Equal(HttpStatusCode.OK, (await server.Client.GetAsync("/health")).StatusCode);
    }

    [Fact]
    public async Task WithoutAWebRoot_TheServerStillRuns()
    {
        // A backend contributor who never ran npm must still get a working server.
        await using var server = await TestApiServer.StartAsync();

        Assert.Equal(HttpStatusCode.OK, (await server.Client.GetAsync("/health")).StatusCode);
    }

    public ValueTask DisposeAsync()
    {
        Directory.Delete(_root, recursive: true);

        return ValueTask.CompletedTask;
    }
}
