using System.Net;
using System.Net.Http.Json;
using Moongate.Core.Types;
using Moongate.Http.Plugin.Data.Api.Plugins;
using Moongate.Tests.Support;

namespace Moongate.Tests.Http.Endpoints.Plugins;

public class PluginAdminEndpointsTests
{
    private const string Route = "/api/v1/admin/plugins";
    private const string HttpPluginId = "moongate.http.plugin";
    private const string HostId = "moongate.host";

    [Fact]
    public async Task Plugins_WithoutAToken_Is401()
    {
        await using var server = await TestApiServer.StartAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, (await server.Client.GetAsync(Route)).StatusCode);
    }

    [Fact]
    public async Task Plugins_WithPlayerToken_Is403()
    {
        await using var server = await TestApiServer.StartAsync(AccountLevelType.Player);
        await server.AuthenticateAsync();

        Assert.Equal(HttpStatusCode.Forbidden, (await server.Client.GetAsync(Route)).StatusCode);
    }

    [Fact]
    public async Task Plugins_WithStaffToken_ListsTheActivatedPlugins()
    {
        await using var server = await TestApiServer.StartAsync();
        await server.AuthenticateAsync();

        var plugins = await GetPluginsAsync(server);

        var http = Assert.Single(plugins, plugin => plugin.Id == HttpPluginId);

        Assert.Equal("Moongate.Http.Plugin", http.Assembly);
        Assert.False(http.IsExternal);
    }

    [Fact]
    public async Task Plugins_ReportsRoutesWithTheirVerbAndPolicy()
    {
        await using var server = await TestApiServer.StartAsync();
        await server.AuthenticateAsync();

        var plugins = await GetPluginsAsync(server);
        var http = Assert.Single(plugins, plugin => plugin.Id == HttpPluginId);

        var status = Assert.Single(http.Routes, route => route.Path == "/api/v1/admin/status");

        Assert.Equal("GET", status.Method);
        Assert.Equal("admin", status.Policy);

        var login = Assert.Single(http.Routes, route => route.Path == "/api/v1/auth/login");

        Assert.Equal("POST", login.Method);
        Assert.Null(login.Policy);
    }

    [Fact]
    public async Task Plugins_AttributesHealthToTheHttpPluginNotToTheHost()
    {
        // /health is a lambda inside HttpServerService, so its closure lives in the HTTP plugin's assembly
        // and the route belongs to that plugin. Getting this wrong is the easy mistake here.
        await using var server = await TestApiServer.StartAsync();
        await server.AuthenticateAsync();

        var plugins = await GetPluginsAsync(server);
        var http = Assert.Single(plugins, plugin => plugin.Id == HttpPluginId);

        Assert.Contains(http.Routes, route => route.Path == "/health");

        var host = Assert.Single(plugins, plugin => plugin.Id == HostId);

        Assert.DoesNotContain(host.Routes, route => route.Path == "/health");
    }

    [Fact]
    public async Task Plugins_LeavesNoApiRouteUnattributed()
    {
        // The invariant that matters: every /api/ route belongs to a catalogued plugin. It breaks if
        // endpoints are ever registered from an assembly nobody records, or if attribution stops working.
        await using var server = await TestApiServer.StartAsync();
        await server.AuthenticateAsync();

        var plugins = await GetPluginsAsync(server);
        var host = Assert.Single(plugins, plugin => plugin.Id == HostId);

        Assert.DoesNotContain(host.Routes, route => route.Path.StartsWith("/api/", StringComparison.Ordinal));
    }

    private static async Task<IReadOnlyList<PluginInfoResponse>> GetPluginsAsync(TestApiServer server)
    {
        var response = await server.Client.GetAsync(Route);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<List<PluginInfoResponse>>())!;
    }
}
