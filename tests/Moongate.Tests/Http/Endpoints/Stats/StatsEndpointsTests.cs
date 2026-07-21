using System.Net;
using System.Net.Http.Json;
using Moongate.Http.Plugin.Data.Api.Stats;
using Moongate.Tests.Support;

namespace Moongate.Tests.Http.Endpoints.Stats;

public sealed class StatsEndpointsTests
{
    [Fact]
    public async Task Stats_IsPublic_AndGroupsTheSnapshot()
    {
        await using var server = await TestApiServer.StartAsync();
        server.Stats.Current = new(
            new DateTimeOffset(2026, 7, 21, 8, 31, 0, TimeSpan.Zero),
            TimeSpan.FromHours(12),
            OnlinePlayers: 12,
            Connections: 15,
            Accounts: 340,
            ActiveAccounts: 300,
            Characters: 512,
            Npcs: 1840,
            WorldItems: 27310,
            ItemTemplates: 412,
            MobileTemplates: 19
        );

        var response = await server.Client.GetAsync("/api/v1/stats"); // no auth header
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var stats = await response.Content.ReadFromJsonAsync<ServerStatsResponse>();
        Assert.Equal(43200, stats!.UptimeSeconds);
        Assert.Equal(12, stats.Players.Online);
        Assert.Equal(15, stats.Players.Connections);
        Assert.Equal(340, stats.Accounts.Total);
        Assert.Equal(300, stats.Accounts.Active);
        Assert.Equal(512, stats.Accounts.Characters);
        Assert.Equal(1840, stats.World.Npcs);
        Assert.Equal(27310, stats.World.Items);
        Assert.Equal(412, stats.Content.ItemTemplates);
        Assert.Equal(19, stats.Content.MobileTemplates);
    }

    [Fact]
    public async Task Stats_BeforeTheFirstRefresh_AnswersOkWithAnUncomputedTimestamp()
    {
        await using var server = await TestApiServer.StartAsync();

        var response = await server.Client.GetAsync("/api/v1/stats");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var stats = await response.Content.ReadFromJsonAsync<ServerStatsResponse>();
        Assert.Equal(DateTimeOffset.MinValue, stats!.GeneratedAt);
        Assert.Equal(0, stats.Players.Online);
    }

    [Fact]
    public async Task Stats_CachesForTheRefreshInterval()
    {
        await using var server = await TestApiServer.StartAsync();

        var response = await server.Client.GetAsync("/api/v1/stats");

        Assert.Equal("public, max-age=30", response.Headers.CacheControl!.ToString());
    }
}
