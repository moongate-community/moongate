using System.Net;
using System.Net.Http.Json;
using Moongate.Http.Plugin.Data.Api.Registration;
using Moongate.Server.Abstractions.Data;
using Moongate.Tests.Support;
using Xunit;

namespace Moongate.Tests.Http.Endpoints.Registration;

public sealed class RegistrationEndpointsTests
{
    [Fact]
    public async Task Register_WhenDisabled_Is403()
    {
        await using var server = await TestApiServer.StartAsync(); // default: registration disabled

        var response = await server.Client.PostAsJsonAsync(
            "/api/v1/register",
            new RegisterRequest("newbie", "secret", "new@bie.test")
        );

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Register_WhenEnabled_CreatesInactive_202()
    {
        await using var server = await TestApiServer.StartAsync();
        server.ServerSettings.Update(new ServerSettingsUpdate { RegistrationEnabled = true });

        var response = await server.Client.PostAsJsonAsync(
            "/api/v1/register",
            new RegisterRequest("newbie", "secret", "new@bie.test")
        );

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.False(server.Accounts.GetByUsername("newbie")!.IsActive);
    }

    [Fact]
    public async Task Verify_ActivatesAccount()
    {
        await using var server = await TestApiServer.StartAsync();
        server.ServerSettings.Update(new ServerSettingsUpdate { RegistrationEnabled = true });
        var token = server.Accounts.RegisterPending("newbie", "secret", "new@bie.test").Token!;

        var response = await server.Client.PostAsJsonAsync("/api/v1/register/verify", new VerifyEmailRequest(token));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(server.Accounts.GetByUsername("newbie")!.IsActive);
    }

    [Fact]
    public async Task Verify_BadToken_Is400()
    {
        await using var server = await TestApiServer.StartAsync();

        var response = await server.Client.PostAsJsonAsync("/api/v1/register/verify", new VerifyEmailRequest("nope"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_RateLimited_429()
    {
        // The fixture seeds a limiter of 2/window; the 3rd call from the same loopback IP is throttled.
        await using var server = await TestApiServer.StartAsync();
        server.ServerSettings.Update(new ServerSettingsUpdate { RegistrationEnabled = true });

        await server.Client.PostAsJsonAsync("/api/v1/register", new RegisterRequest("a", "secret", "a@b.test"));
        await server.Client.PostAsJsonAsync("/api/v1/register", new RegisterRequest("b", "secret", "b@b.test"));
        var third = await server.Client.PostAsJsonAsync("/api/v1/register", new RegisterRequest("c", "secret", "c@b.test"));

        Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);
    }
}
