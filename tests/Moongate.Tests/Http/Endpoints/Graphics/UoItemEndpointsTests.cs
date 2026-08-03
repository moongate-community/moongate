using System.Net;
using System.Net.Http.Json;
using Moongate.Core.Types;
using Moongate.Http.Plugin.Data;
using Moongate.Http.Plugin.Data.Api.Graphics;
using Moongate.Http.Plugin.Endpoints.Graphics;
using Moongate.Http.Plugin.Extensions;
using Moongate.Tests.Support;
using Moongate.Ultima.Tiles;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Http.Endpoints.Graphics;

/// <summary>
/// The raw tile catalogue: what the client files say about a graphic, before any template has an
/// opinion. The search and the flag filter are pure, so they are driven straight rather than through
/// the process-wide tiledata the route reads, which any test in the run may have filled.
/// </summary>
public class UoItemEndpointsTests
{
    private const string Route = "/api/v1/admin/uo-items";

    [Fact]
    public void Matches_FindsATileByItsDecimalId()
        => Assert.True(UoItemEndpoints.Matches(3821, Tile("gold coin"), "3821"));

    [Fact]
    public void Matches_FindsATileByItsHexId()
        => Assert.True(UoItemEndpoints.Matches(3821, Tile("gold coin"), "0xEED"));

    [Fact]
    public void Matches_FindsATileByName()
        => Assert.True(UoItemEndpoints.Matches(3821, Tile("gold coin"), "COIN"));

    [Fact]
    public void Matches_ATileThatAnswersNothing_IsExcluded()
        => Assert.False(UoItemEndpoints.Matches(3821, Tile("gold coin"), "dagger"));

    // A nameless tile is an unused id. It still has art, so a search by number must reach it.
    [Fact]
    public void Matches_ANamelessTile_IsStillFoundByNumber()
        => Assert.True(UoItemEndpoints.Matches(9, Tile(null!), "9"));

    [Fact]
    public void Matches_NoSearch_KeepsEverything()
        => Assert.True(UoItemEndpoints.Matches(3821, Tile("gold coin"), null));

    [Theory, InlineData("Container"), InlineData("container"), InlineData("CONTAINER")]
    public void TryParseFlag_ReadsAFlagWhateverItsCase(string name)
    {
        Assert.True(UoItemEndpoints.TryParseFlag(name, out var flag));
        Assert.Equal(TileFlagType.Container, flag);
    }

    // None would pass every tile through, so it must not be reachable by name: filtering by it would
    // silently mean "no filter" while reading like a real one.
    [Theory, InlineData("None"), InlineData("Nonsense"), InlineData("")]
    public void TryParseFlag_RefusesWhatIsNotAFlag(string name)
        => Assert.False(UoItemEndpoints.TryParseFlag(name, out _));

    [Fact]
    public void Summary_CarriesTheArtUrlAndTheSetFlags()
    {
        var summary = UoItemSummary.From(3821, Tile("gold coin", TileFlagType.Generic | TileFlagType.Container));

        Assert.Equal("0x0EED", summary.Hex);
        Assert.Equal("/api/v1/images/items/0x0EED.png", summary.ImageUrl);
        Assert.Contains("Generic", summary.Flags);
        Assert.Contains("Container", summary.Flags);
    }

    [Fact]
    public void Summary_ATileWithNoFlags_ListsNone()
        => Assert.Empty(UoItemSummary.From(9, Tile("nothing")).Flags);

    // Asserting an empty catalogue would only hold in a run where nothing has loaded the client
    // tables, and they are process-wide: another test file filling them would fail this one. What the
    // route promises is a page either way, never an error, and that holds whatever is loaded.
    [Fact]
    public async Task List_IsAlwaysAPageAndNeverAnError()
    {
        await using var server = await StartAsync();

        await server.AuthenticateAsync();

        var response = await server.Client.GetAsync(Route);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await response.Content.ReadFromJsonAsync<PagedResponse<UoItemSummary>>();

        Assert.NotNull(page);
        Assert.InRange(page.Items.Count, 0, page.PageSize);
        Assert.True(page.Total >= page.Items.Count);
    }

    [Fact]
    public async Task List_AnUnknownFlag_IsBadRequest()
    {
        await using var server = await StartAsync();

        await server.AuthenticateAsync();

        Assert.Equal(HttpStatusCode.BadRequest, (await server.Client.GetAsync($"{Route}?flag=nonsense")).StatusCode);
    }

    [Theory, InlineData("?page=0"), InlineData("?pageSize=5000")]
    public async Task List_OutOfRangePaging_IsBadRequest(string query)
    {
        await using var server = await StartAsync();

        await server.AuthenticateAsync();

        Assert.Equal(HttpStatusCode.BadRequest, (await server.Client.GetAsync($"{Route}{query}")).StatusCode);
    }

    [Fact]
    public async Task List_AsAPlayer_IsForbidden()
    {
        await using var server = await StartAsync(AccountLevelType.Player);

        await server.AuthenticateAsync();

        Assert.Equal(HttpStatusCode.Forbidden, (await server.Client.GetAsync(Route)).StatusCode);
    }

    [Fact]
    public async Task List_WithoutAToken_IsUnauthorized()
    {
        await using var server = await StartAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, (await server.Client.GetAsync(Route)).StatusCode);
    }

    private static ItemData Tile(string name, TileFlagType flags = TileFlagType.None)
        => new() { Name = name, Flags = flags };

    private static Task<TestApiServer> StartAsync(AccountLevelType level = AccountLevelType.Administrator)
        => TestApiServer.StartAsync(
            level,
            configure: container => container.RegisterApiEndpointInstance(new UoItemEndpoints())
        );
}
