using System.Net;
using System.Net.Http.Json;
using Moongate.Core.Types;
using Moongate.Http.Plugin.Data;
using Moongate.Http.Plugin.Data.Api.Graphics;
using Moongate.Http.Plugin.Endpoints.Graphics;
using Moongate.Http.Plugin.Extensions;
using Moongate.Tests.Support;
using Moongate.Ultima.Graphics;

namespace Moongate.Tests.Http.Endpoints.Graphics;

/// <summary>
/// The hue catalogue. The colour maths and the search are pure and driven straight; the route itself
/// reads the process-wide hue table, which is empty in a run with no client files.
/// </summary>
public class HueEndpointsTests
{
    private const string Route = "/api/v1/admin/hues";

    [Fact]
    public void Matches_FindsAHueByNumber()
        => Assert.True(HueEndpoints.Matches(1002, Named("blood red"), "1002"));

    [Fact]
    public void Matches_FindsAHueByName()
        => Assert.True(HueEndpoints.Matches(1002, Named("blood red"), "RED"));

    [Fact]
    public void Matches_AHueThatAnswersNothing_IsExcluded()
        => Assert.False(HueEndpoints.Matches(1002, Named("blood red"), "azure"));

    [Fact]
    public void Matches_NoSearch_KeepsEverything()
        => Assert.True(HueEndpoints.Matches(1002, Named("blood red"), null));

    // RGB555 with the top bit clear: 0x7C00 is pure red, and expanding 5 bits to 8 has to reach FF
    // rather than F8, or every swatch comes out slightly dark.
    [Theory]
    [InlineData(0x7C00, "#FF0000")]
    [InlineData(0x03E0, "#00FF00")]
    [InlineData(0x001F, "#0000FF")]
    [InlineData(0x0000, "#000000")]
    [InlineData(0x7FFF, "#FFFFFF")]
    public void ToHex_ExpandsTheFiveBitChannels(int color, string expected)
        => Assert.Equal(expected, HueSummary.ToHex((ushort)color));

    // The table is 3000 long with or without hues.mul; the unread entries are all-zero and must not
    // reach the catalogue as three thousand identical black swatches.
    [Fact]
    public void IsLoaded_AnEntryThatPaintsNothing_IsNotAHue()
        => Assert.False(HueEndpoints.IsLoaded(Named("Null")));

    [Fact]
    public void IsLoaded_AnEntryWithColour_IsAHue()
        => Assert.True(HueEndpoints.IsLoaded(Painted("blood red")));

    [Fact]
    public void Summary_ANamelessHue_IsNamedByItsNumber()
        => Assert.Equal("Hue 1002", HueSummary.From(1002, Named("   ")).Name);

    [Fact]
    public void Summary_CarriesAGradientOfTheRamp()
        => Assert.Equal(HueSummary.GradientSteps, HueSummary.From(1002, Painted("blood red")).Gradient.Count);

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

        var page = await response.Content.ReadFromJsonAsync<PagedResponse<HueSummary>>();

        Assert.NotNull(page);
        Assert.InRange(page.Items.Count, 0, page.PageSize);
        Assert.True(page.Total >= page.Items.Count);
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

    private static Hue Named(string name)
    {
        var hue = new Hue(0) { Name = name };

        return hue;
    }

    /// <summary>A hue with an actual ramp in it, the way one read from hues.mul looks.</summary>
    private static Hue Painted(string name)
    {
        var hue = Named(name);

        for (var step = 0; step < hue.Colors.Length; step++)
        {
            hue.Colors[step] = (ushort)(0x0400 + step);
        }

        return hue;
    }

    private static Task<TestApiServer> StartAsync(AccountLevelType level = AccountLevelType.Administrator)
        => TestApiServer.StartAsync(
            level,
            configure: container => container.RegisterApiEndpointInstance(new HueEndpoints())
        );
}
