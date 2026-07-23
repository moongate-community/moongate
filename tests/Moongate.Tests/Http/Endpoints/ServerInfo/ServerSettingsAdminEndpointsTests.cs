using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Moongate.Http.Plugin.Data.Api.ServerInfo;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Types;
using Moongate.Tests.Support;
using Xunit;

namespace Moongate.Tests.Http.Endpoints.ServerInfo;

public sealed class ServerSettingsAdminEndpointsTests
{
    [Fact]
    public async Task Get_RequiresAdmin()
    {
        await using var server = await TestApiServer.StartAsync();

        var response = await server.Client.GetAsync("/api/v1/admin/server-settings"); // no token
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Put_UpdatesSettings()
    {
        await using var server = await TestApiServer.StartAsync();
        await server.AuthenticateAsync();

        var response = await server.Client.PutAsJsonAsync(
            "/api/v1/admin/server-settings",
            new UpdateServerSettingsRequest { RegistrationEnabled = true, Description = "Hi", Tagline = "Welcome" }
        );
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.True(server.ServerSettings.Get().RegistrationEnabled);
        Assert.Equal("Hi", server.ServerSettings.Get().Description);
        Assert.Equal("Welcome", server.ServerSettings.Get().Tagline);
    }

    [Fact]
    public async Task UploadAsset_StoresAndServes()
    {
        await using var server = await TestApiServer.StartAsync();
        await server.AuthenticateAsync();

        using var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(new byte[] { 1, 2, 3 });
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(file, "file", "logo.png");

        var upload = await server.Client.PostAsync("/api/v1/admin/server-settings/assets/logo", content);
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);

        // now public serving finds it
        var served = await server.Client.GetAsync("/api/v1/server-info/assets/logo");
        Assert.Equal(HttpStatusCode.OK, served.StatusCode);
        Assert.Equal("image/png", served.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task UploadAsset_RejectsUnsupportedType()
    {
        await using var server = await TestApiServer.StartAsync();
        await server.AuthenticateAsync();

        using var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(new byte[] { 1 });
        file.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
        content.Add(file, "file", "x.zip");

        var upload = await server.Client.PostAsync("/api/v1/admin/server-settings/assets/logo", content);
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, upload.StatusCode);
    }

    [Fact]
    public async Task DeleteAsset_RemovesIt()
    {
        await using var server = await TestApiServer.StartAsync();
        await server.AuthenticateAsync();
        server.ServerSettings.SetAsset(
            ServerAssetSlotType.Logo,
            new ServerAssetMeta { FileName = "Logo.png", ContentType = "image/png" }
        );

        var response = await server.Client.DeleteAsync("/api/v1/admin/server-settings/assets/logo");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.DoesNotContain("Logo", server.ServerSettings.Get().Assets.Keys);
    }
}
