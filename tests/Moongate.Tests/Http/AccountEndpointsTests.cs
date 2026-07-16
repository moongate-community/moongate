using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Moongate.Core.Types;
using Moongate.Http.Plugin.Data;
using Moongate.Tests.Support;

namespace Moongate.Tests.Http;

public class AccountEndpointsTests
{
    [Fact]
    public async Task List_ReportsEveryAccount()
    {
        await using var server = await TestApiServer.StartAsync();
        await AuthenticateAsync(server);

        var response = await server.Client.GetAsync("/api/v1/admin/accounts");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("tom", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_KnownAccount_ReportsIt()
    {
        await using var server = await TestApiServer.StartAsync();
        await AuthenticateAsync(server);

        var response = await server.Client.GetAsync("/api/v1/admin/accounts/tom");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Administrator", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_UnknownAccount_Is404()
    {
        await using var server = await TestApiServer.StartAsync();
        await AuthenticateAsync(server);

        Assert.Equal(
            HttpStatusCode.NotFound,
            (await server.Client.GetAsync("/api/v1/admin/accounts/nobody")).StatusCode
        );
    }

    [Fact]
    public async Task Responses_NeverCarryTheSecrets()
    {
        // Against the raw JSON, not a deserialized DTO: deserializing into AccountResponse would drop an
        // extra field silently, which is exactly the leak this guards. AccountEntity holds both.
        await using var server = await TestApiServer.StartAsync();
        await AuthenticateAsync(server);

        foreach (var route in new[] { "/api/v1/admin/accounts", "/api/v1/admin/accounts/tom" })
        {
            var body = await server.Client.GetStringAsync(route);

            Assert.DoesNotContain("assword", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("ctivationToken", body, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task List_WithoutAToken_Is401()
    {
        await using var server = await TestApiServer.StartAsync();

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await server.Client.GetAsync("/api/v1/admin/accounts")).StatusCode
        );
    }

    [Fact]
    public async Task List_WithPlayerToken_Is403()
    {
        await using var server = await TestApiServer.StartAsync(AccountLevelType.Player);
        await AuthenticateAsync(server);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await server.Client.GetAsync("/api/v1/admin/accounts")).StatusCode
        );
    }

    internal static async Task AuthenticateAsync(TestApiServer server)
    {
        var response = await server.Client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { username = "tom", password = "secret" }
        );
        var token = await response.Content.ReadFromJsonAsync<ApiTokenResult>();

        server.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
    }
}
