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

    [Fact]
    public async Task Create_NewAccount_Is201WithLocation()
    {
        await using var server = await TestApiServer.StartAsync();
        await AuthenticateAsync(server);

        var response = await server.Client.PostAsJsonAsync(
            "/api/v1/admin/accounts",
            new { username = "alice", password = "secret", email = "a@b.c", level = "Player" }
        );

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("/api/v1/admin/accounts/alice", response.Headers.Location?.ToString());
        Assert.NotNull(server.Accounts.GetByUsername("alice"));
    }

    [Fact]
    public async Task Create_OmittedLevel_DefaultsToPlayer()
    {
        // The safe default: an account that gains staff rights by accident is the wrong way to fail.
        await using var server = await TestApiServer.StartAsync();
        await AuthenticateAsync(server);

        await server.Client.PostAsJsonAsync(
            "/api/v1/admin/accounts",
            new { username = "alice", password = "secret" }
        );

        Assert.Equal(AccountLevelType.Player, server.Accounts.GetByUsername("alice")!.AccountLevel);
    }

    [Fact]
    public async Task Create_TakenUsername_Is409()
    {
        await using var server = await TestApiServer.StartAsync();
        await AuthenticateAsync(server);

        var response = await server.Client.PostAsJsonAsync(
            "/api/v1/admin/accounts",
            new { username = "tom", password = "secret" }
        );

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Theory]
    [InlineData("", "secret")]
    [InlineData("alice", "")]
    public async Task Create_EmptyUsernameOrPassword_Is400(string username, string password)
    {
        await using var server = await TestApiServer.StartAsync();
        await AuthenticateAsync(server);

        var response = await server.Client.PostAsJsonAsync(
            "/api/v1/admin/accounts",
            new { username, password }
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_UnknownLevel_Is400AndCreatesNothing()
    {
        // The level is checked before the write, so a bad request cannot half-apply.
        await using var server = await TestApiServer.StartAsync();
        await AuthenticateAsync(server);

        var response = await server.Client.PostAsJsonAsync(
            "/api/v1/admin/accounts",
            new { username = "alice", password = "secret", level = "Wizard" }
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(server.Accounts.GetByUsername("alice"));
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
