using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Moongate.Http.Plugin.Data;
using Moongate.Http.Plugin.Data.Api.Graphics;
using Moongate.Http.Plugin.Interfaces.Endpoints;
using Moongate.Http.Plugin.Services.Hosting;
using Moongate.Ultima.Graphics;

namespace Moongate.Http.Plugin.Endpoints.Graphics;

/// <summary>The staff view of hues.mul: every dye the client knows, with the colours it paints.</summary>
public sealed class HueEndpoints : IApiEndpointRegistration
{
    public void Register(IEndpointRouteBuilder routes)

        // A method group, not a lambda: Swashbuckle reads the /// off the handler's method, and a lambda
        // has none — the route would document itself blank.
        => routes.MapGet("/api/v1/admin/hues", List)
                 .WithName("ListHues")
                 .WithTags("graphics")
                 .Produces<PagedResponse<HueSummary>>()
                 .RequireAuthorization(HttpServerService.AdminPolicy);

    /// <summary>
    /// True when a hue was actually read from hues.mul. The table is 3000 entries long whether or not
    /// the file was there, and the ones that were not read paint nothing at all — listing them would
    /// offer three thousand identical black swatches on a shard running without client files.
    /// </summary>
    public static bool IsLoaded(Hue hue)
        => hue.Colors is { Length: > 0 } colors && colors.Any(color => color != 0);

    /// <summary>True when a hue answers the search: its number, or any part of its name.</summary>
    public static bool Matches(int value, Hue hue, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return true;
        }

        var term = search.Trim();

        return value.ToString(CultureInfo.InvariantCulture).Contains(term, StringComparison.OrdinalIgnoreCase) ||
               (hue.Name?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    /// <summary>Every hue the client files describe, paged.</summary>
    /// <remarks>
    /// Values are 1-based, the way packets and the image routes take them: hue 1 is the first row of
    /// hues.mul and hue 0 means unhued, so 0 is not listed. Each row carries a representative colour for
    /// a swatch and eight evenly spaced steps of the full ramp. Search matches the number or the name.
    /// Without client files the catalogue is empty rather than an error, because a shard can run
    /// headless — the hue table exists either way, but its unread entries paint nothing and are left
    /// out.
    /// </remarks>
    private IResult List(string? page, string? pageSize, string? search)
    {
        if (!PageRequest.TryParse(page, pageSize, search, out var request, out var error))
        {
            return Results.Problem(error, statusCode: StatusCodes.Status400BadRequest);
        }

        var table = Hues.List ?? [];
        var matched = table
                      .Select((hue, index) => (Value: index + 1, Hue: hue))
                      .Where(entry => IsLoaded(entry.Hue))
                      .Where(entry => Matches(entry.Value, entry.Hue, request.Search))
                      .ToArray();

        IReadOnlyList<HueSummary> items =
        [
            .. matched.Skip(request.Skip)
                      .Take(request.PageSize)
                      .Select(entry => HueSummary.From(entry.Value, entry.Hue))
        ];

        return Results.Ok(PagedResponse<HueSummary>.From(items, matched.Length, request));
    }
}
