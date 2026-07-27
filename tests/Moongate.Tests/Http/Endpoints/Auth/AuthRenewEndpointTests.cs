using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using Moongate.Core.Types;
using Moongate.Http.Plugin.Data;
using Moongate.Tests.Support;

namespace Moongate.Tests.Http.Endpoints.Auth;

public class AuthRenewEndpointTests
{
    private const string Route = "/api/v1/auth/renew";

    [Fact]
    public async Task Renew_WithoutAToken_Is401()
    {
        await using var server = await TestApiServer.StartAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, (await server.Client.PostAsync(Route, null)).StatusCode);
    }

    [Fact]
    public async Task Renew_WithAValidToken_ReturnsAFreshToken()
    {
        var clock = MutableTimeProvider.StartingNow();
        await using var server = await TestApiServer.StartAsync(clock: clock);
        await server.AuthenticateAsync();

        // Move on inside the session so the new token's expiry genuinely differs from the old one.
        clock.Advance(TimeSpan.FromMinutes(30));

        var response = await server.Client.PostAsync(Route, null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var renewed = await response.Content.ReadFromJsonAsync<ApiTokenResult>();

        Assert.False(string.IsNullOrWhiteSpace(renewed.Token));
        Assert.Equal(clock.GetUtcNow().AddMinutes(60), renewed.ExpiresAt);
    }

    [Fact]
    public async Task Renew_KeepsTheOriginalAuthTime()
    {
        // The point of the whole design: renewing must not restart the session clock, or the absolute cap
        // could never be reached.
        var clock = MutableTimeProvider.StartingNow();
        await using var server = await TestApiServer.StartAsync(clock: clock);
        await server.AuthenticateAsync();

        var loginAuthTime = AuthTimeOf(server.Client.DefaultRequestHeaders.Authorization!.Parameter!);

        clock.Advance(TimeSpan.FromMinutes(45));

        var renewed = (await (await server.Client.PostAsync(Route, null)).Content
            .ReadFromJsonAsync<ApiTokenResult>()).Token;

        Assert.Equal(loginAuthTime, AuthTimeOf(renewed));
    }

    [Fact]
    public async Task Renew_PastTheSessionCap_Is401()
    {
        var clock = MutableTimeProvider.StartingNow();
        await using var server = await TestApiServer.StartAsync(clock: clock);
        await server.AuthenticateAsync();

        // Default cap is 12 hours. Renew once inside it so the chain is alive, then step past it.
        clock.Advance(TimeSpan.FromMinutes(30));
        Assert.Equal(HttpStatusCode.OK, (await server.Client.PostAsync(Route, null)).StatusCode);

        clock.Advance(TimeSpan.FromHours(12));

        Assert.Equal(HttpStatusCode.Unauthorized, (await server.Client.PostAsync(Route, null)).StatusCode);
    }

    [Fact]
    public async Task Renew_ForADeactivatedAccount_Is401()
    {
        // Coarse revocation: the account is re-read rather than trusted from the claims, so suspending it
        // stops renewals even though the token it presents is still cryptographically valid.
        var clock = MutableTimeProvider.StartingNow();
        await using var server = await TestApiServer.StartAsync(clock: clock);
        await server.AuthenticateAsync();

        Assert.True(server.Accounts.SetActive("tom", false));

        clock.Advance(TimeSpan.FromMinutes(5));

        Assert.Equal(HttpStatusCode.Unauthorized, (await server.Client.PostAsync(Route, null)).StatusCode);
    }

    [Fact]
    public async Task Renew_PicksUpALevelChange()
    {
        var clock = MutableTimeProvider.StartingNow();
        await using var server = await TestApiServer.StartAsync(AccountLevelType.Player, clock: clock);
        await server.AuthenticateAsync();

        // A player token cannot reach an admin route.
        Assert.Equal(HttpStatusCode.Forbidden, (await server.Client.GetAsync("/api/v1/admin/status")).StatusCode);

        Assert.True(server.Accounts.SetLevel("tom", AccountLevelType.Administrator));

        clock.Advance(TimeSpan.FromMinutes(5));

        var renewed = await (await server.Client.PostAsync(Route, null)).Content.ReadFromJsonAsync<ApiTokenResult>();

        server.Client.DefaultRequestHeaders.Authorization = new("Bearer", renewed.Token);

        Assert.Equal(HttpStatusCode.OK, (await server.Client.GetAsync("/api/v1/admin/status")).StatusCode);
    }

    /// <summary>Reads the raw <c>auth_time</c> claim out of a token without validating it.</summary>
    private static string AuthTimeOf(string token)
        => new JwtSecurityTokenHandler().ReadJwtToken(token).Claims.Single(c => c.Type == "auth_time").Value;
}
