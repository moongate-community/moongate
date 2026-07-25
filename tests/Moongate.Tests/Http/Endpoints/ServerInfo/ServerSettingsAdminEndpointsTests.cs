using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
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
            new UpdateServerSettingsRequest
            {
                RegistrationEnabled = true,
                Description = "Hi",
                Tagline = "Welcome",
                Contacts = new("https://shard.example", null, null)
            }
        );
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.True(server.ServerSettings.Get().RegistrationEnabled);
        Assert.Equal("Hi", server.ServerSettings.Get().Description);
        Assert.Equal("Welcome", server.ServerSettings.Get().Tagline);
    }

    [Fact]
    public async Task Get_ReturnsPersistedToggleAndRegistrationReadiness()
    {
        await using var server = await TestApiServer.StartAsync();
        await server.AuthenticateAsync();
        server.ServerSettings.Update(
            new()
            {
                RegistrationEnabled = true,
                Contacts = new() { Website = "https://shard.example" }
            }
        );

        var response = await server.Client.GetAsync("/api/v1/admin/server-settings");
        var settings = await response.Content.ReadFromJsonAsync<ServerSettingsResponse>();

        Assert.True(settings!.RegistrationEnabled);
        Assert.True(settings.RegistrationReadiness.Ready);
        Assert.True(settings.RegistrationReadiness.WebsiteValid);
        Assert.True(settings.RegistrationReadiness.EmailChannelSelected);
        Assert.True(settings.RegistrationReadiness.EmailChannelAvailable);
    }

    [Theory]
    [InlineData("ftp://shard.example", "email", true)]
    [InlineData("https://shard.example", "log", true)]
    [InlineData("https://shard.example", "email", false)]
    public async Task Put_EnablingWithoutReadiness_RejectsAndPersistsNothing(
        string website,
        string accountVerificationChannel,
        bool emailChannelReady
    )
    {
        await using var server = await TestApiServer.StartAsync(
            emailChannelReady: emailChannelReady,
            accountVerificationChannel: accountVerificationChannel
        );
        await server.AuthenticateAsync();

        var response = await server.Client.PutAsJsonAsync(
            "/api/v1/admin/server-settings",
            new UpdateServerSettingsRequest
            {
                Description = "Must not persist",
                RegistrationEnabled = true,
                Contacts = new(website, null, null)
            }
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Contains("registrationEnabled", problem!.Errors.Keys);
        Assert.False(server.ServerSettings.Get().RegistrationEnabled);
        Assert.Null(server.ServerSettings.Get().Description);
        Assert.Null(server.ServerSettings.Get().Contacts.Website);
    }

    [Fact]
    public async Task Put_DisablingAnUnhealthyEnabledState_Succeeds()
    {
        await using var server = await TestApiServer.StartAsync();
        await server.AuthenticateAsync();
        server.ServerSettings.Update(new() { RegistrationEnabled = true });

        var response = await server.Client.PutAsJsonAsync(
            "/api/v1/admin/server-settings",
            new UpdateServerSettingsRequest { RegistrationEnabled = false }
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(server.ServerSettings.Get().RegistrationEnabled);
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
