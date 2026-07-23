using System.Net;
using System.Net.Http.Json;
using Moongate.Http.Plugin.Data.Api.ServerInfo;
using Moongate.Server.Abstractions.Data;
using Moongate.Tests.Support;
using Xunit;

namespace Moongate.Tests.Http.Endpoints.ServerInfo;

public sealed class ServerInfoEndpointsTests
{
    [Fact]
    public async Task ServerInfo_IsPublic_AndReflectsSettings()
    {
        await using var server = await TestApiServer.StartAsync();
        server.ServerSettings.Update(
            new ServerSettingsUpdate { Description = "A fun shard", Tagline = "Sosaria never sleeps.", RegistrationEnabled = true }
        );

        var response = await server.Client.GetAsync("/api/v1/server-info"); // no auth header
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var info = await response.Content.ReadFromJsonAsync<ServerInfoResponse>();
        Assert.Equal("Moongate", info!.ShardName);
        Assert.Equal("A fun shard", info.Description);
        Assert.Equal("Sosaria never sleeps.", info.Tagline);
        Assert.True(info.RegistrationEnabled);
    }

    [Fact]
    public async Task Asset_MissingSlot_Is404()
    {
        await using var server = await TestApiServer.StartAsync();

        var response = await server.Client.GetAsync("/api/v1/server-info/assets/logo");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Asset_UnknownSlot_Is400()
    {
        await using var server = await TestApiServer.StartAsync();

        var response = await server.Client.GetAsync("/api/v1/server-info/assets/wallpaper");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
