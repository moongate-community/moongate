using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Moongate.Core.Types;
using Moongate.Http.Plugin.Data;
using Moongate.Tests.Support;

namespace Moongate.Tests.Http.Endpoints.Admin;

public class AdminEndpointsTests
{
    [Fact]
    public async Task AdminStatus_WithoutAToken_Is401()
    {
        await using var server = await TestApiServer.StartAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, (await server.Client.GetAsync("/api/v1/admin/status")).StatusCode);
    }

    [Theory]
    [InlineData(AccountLevelType.Administrator)]
    [InlineData(AccountLevelType.GrandMaster)]
    public async Task AdminStatus_WithStaffToken_Is200(AccountLevelType level)
    {
        await using var server = await TestApiServer.StartAsync(level);
        await server.AuthenticateAsync();

        Assert.Equal(HttpStatusCode.OK, (await server.Client.GetAsync("/api/v1/admin/status")).StatusCode);
    }

    [Fact]
    public async Task AdminStatus_WithPlayerToken_Is403()
    {
        // The test that proves the admin/player split exists rather than being asserted: a valid token
        // that is simply not staff enough.
        await using var server = await TestApiServer.StartAsync(AccountLevelType.Player);
        await server.AuthenticateAsync();

        Assert.Equal(HttpStatusCode.Forbidden, (await server.Client.GetAsync("/api/v1/admin/status")).StatusCode);
    }

    [Fact]
    public async Task PlayerMe_WithAnyToken_ReportsTheAccount()
    {
        await using var server = await TestApiServer.StartAsync(AccountLevelType.Player);
        await server.AuthenticateAsync();

        var response = await server.Client.GetAsync("/api/v1/player/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("tom", body, StringComparison.Ordinal);
        Assert.Contains("Player", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PlayerMe_WithoutAToken_Is401()
    {
        await using var server = await TestApiServer.StartAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, (await server.Client.GetAsync("/api/v1/player/me")).StatusCode);
    }
}
