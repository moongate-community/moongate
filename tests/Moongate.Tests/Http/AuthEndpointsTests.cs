using System.Net;
using System.Net.Http.Json;
using Moongate.Http.Plugin.Data;
using Moongate.Tests.Support;

namespace Moongate.Tests.Http;

public class AuthEndpointsTests
{
    [Fact]
    public async Task Login_GoodCredentials_ReturnsAToken()
    {
        await using var server = await TestApiServer.StartAsync();

        var response = await server.Client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { username = "tom", password = "secret" }
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var token = await response.Content.ReadFromJsonAsync<ApiTokenResult>();
        Assert.False(string.IsNullOrWhiteSpace(token.Token));
    }

    [Fact]
    public async Task Login_ReturnsCamelCaseJson()
    {
        // The wire format is a contract clients are written against, so it is pinned rather than left to
        // whatever the serializer defaults to.
        await using var server = await TestApiServer.StartAsync();

        var response = await server.Client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { username = "tom", password = "secret" }
        );

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"expiresAt\"", body, StringComparison.Ordinal);
        Assert.DoesNotContain("\"ExpiresAt\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Login_WrongPassword_Is401()
    {
        await using var server = await TestApiServer.StartAsync();

        var response = await server.Client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { username = "tom", password = "wrong" }
        );

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_BlockedAccount_IsAlso401AndSaysNothingMore()
    {
        // The game client needs to know why; an HTTP API telling an attacker that a username exists but
        // is blocked is an oracle. Same flat 401 either way.
        await using var server = await TestApiServer.StartAsync();
        server.Accounts.SetActive("tom", false);

        var response = await server.Client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { username = "tom", password = "secret" }
        );

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.DoesNotContain("block", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_UnknownUsername_Is401()
    {
        await using var server = await TestApiServer.StartAsync();

        var response = await server.Client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { username = "nobody", password = "secret" }
        );

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Version_NeedsNoTokenAndNamesTheShard()
    {
        await using var server = await TestApiServer.StartAsync();

        var response = await server.Client.GetAsync("/api/v1/version");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Moongate", await response.Content.ReadAsStringAsync());
    }
}
