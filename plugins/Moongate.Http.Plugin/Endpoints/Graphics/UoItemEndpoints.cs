using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Moongate.Http.Plugin.Data;
using Moongate.Http.Plugin.Data.Api.Graphics;
using Moongate.Http.Plugin.Interfaces.Endpoints;
using Moongate.Http.Plugin.Services.Hosting;
using Moongate.Ultima.Tiles;
using Moongate.Ultima.Types;

namespace Moongate.Http.Plugin.Endpoints.Graphics;

/// <summary>
/// The staff view of tiledata: every item graphic the client knows, whether or not any template ever
/// mentions it. This is what a picker browses when the answer is "some art", not "some template".
/// </summary>
public sealed class UoItemEndpoints : IApiEndpointRegistration
{
    public void Register(IEndpointRouteBuilder routes)

        // A method group, not a lambda: Swashbuckle reads the /// off the handler's method, and a lambda
        // has none — the route would document itself blank.
        => routes.MapGet("/api/v1/admin/uo-items", List)
                 .WithName("ListUoItems")
                 .WithTags("graphics")
                 .Produces<PagedResponse<UoItemSummary>>()
                 .RequireAuthorization(HttpServerService.AdminPolicy);

    /// <summary>True when a tile answers the search: its decimal id, its 0x hex id, or part of its name.</summary>
    public static bool Matches(int itemId, ItemData data, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return true;
        }

        var term = search.Trim();

        // "0xEED" and "0x0EED" are the same tile to whoever typed them, but not to a substring match
        // against the padded form, so an explicit hex term is compared as a number.
        if (term.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(term[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var wanted))
        {
            return itemId == wanted;
        }

        return itemId.ToString(CultureInfo.InvariantCulture).Contains(term, StringComparison.OrdinalIgnoreCase) ||
               $"0x{itemId:X4}".Contains(term, StringComparison.OrdinalIgnoreCase) ||
               (data.Name?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    /// <summary>Reads a flag name into the flag itself, so an unknown name refuses rather than matching all.</summary>
    public static bool TryParseFlag(string? name, out TileFlagType flag)
    {
        flag = TileFlagType.None;

        return !string.IsNullOrWhiteSpace(name) &&
               Enum.TryParse(name.Trim(), ignoreCase: true, out flag) &&
               flag != TileFlagType.None;
    }

    /// <summary>Every item tile the client files describe, paged.</summary>
    /// <remarks>
    /// Rows carry the tile's flags, weight and height, and the URL of its art. Search matches the
    /// decimal id, the 0x-prefixed hex, or the tiledata name. Pass flag to keep only tiles carrying one
    /// — Container, Wearable, Weapon and the rest of TileFlagType — and an unknown flag name is a 400
    /// rather than a silently unfiltered list. Nameless tiles are the unused ids and are listed too,
    /// because their graphic may still be worth picking. Without client files the catalogue is empty
    /// rather than an error, because a shard can run headless.
    /// </remarks>
    private IResult List(string? page, string? pageSize, string? search, string? flag)
    {
        if (!PageRequest.TryParse(page, pageSize, search, out var request, out var error))
        {
            return Results.Problem(error, statusCode: StatusCodes.Status400BadRequest);
        }

        var wanted = TileFlagType.None;

        if (!string.IsNullOrWhiteSpace(flag) && !TryParseFlag(flag, out wanted))
        {
            return Results.Problem($"'{flag}' is not a tile flag.", statusCode: StatusCodes.Status400BadRequest);
        }

        var table = TileData.ItemTable ?? [];
        var matched = table
                      .Select((data, itemId) => (ItemId: itemId, Data: data))
                      .Where(entry => wanted == TileFlagType.None || (entry.Data.Flags & wanted) != 0)
                      .Where(entry => Matches(entry.ItemId, entry.Data, request.Search))
                      .ToArray();

        IReadOnlyList<UoItemSummary> items =
        [
            .. matched.Skip(request.Skip)
                      .Take(request.PageSize)
                      .Select(entry => UoItemSummary.From(entry.ItemId, entry.Data))
        ];

        return Results.Ok(PagedResponse<UoItemSummary>.From(items, matched.Length, request));
    }
}
