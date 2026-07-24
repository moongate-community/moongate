using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Moongate.Http.Plugin.Data;
using Moongate.Http.Plugin.Interfaces.Endpoints;
using Moongate.Http.Plugin.Interfaces.Maps;
using Moongate.Http.Plugin.Types;
using Moongate.Server.Abstractions.Interfaces.World;
using Moongate.UO.Data.Types;

namespace Moongate.Http.Plugin.Endpoints.Maps;

/// <summary>UO map facets as tiles and as whole images.</summary>
public sealed class MapImageEndpoints : IApiEndpointRegistration
{
    private readonly IMapImageService _maps;
    private readonly IUltimaMapProvider _provider;

    public MapImageEndpoints(IMapImageService maps, IUltimaMapProvider provider)
    {
        _maps = maps;
        _provider = provider;
    }

    public void Register(IEndpointRouteBuilder routes)
    {
        // Method groups, not lambdas: Swashbuckle reads the /// off the handler's method, and a lambda has
        // none — the route would document itself blank.
        routes.MapGet("/api/v1/images/maps", List)
            .WithName("ListMaps")
            .WithTags("images")
            .Produces<IReadOnlyList<MapFacetInfo>>()
            .AllowAnonymous();

        // Before the tile route, so "full" is never read as a zoom.
        routes.MapGet("/api/v1/images/maps/{map}/full.png", GetFull)
            .WithName("GetFullMapImage")
            .WithTags("images")
            .Produces<byte[]>(StatusCodes.Status200OK, "image/png")
            .AllowAnonymous();

        routes.MapGet("/api/v1/images/maps/{map}/{z}/{x}/{y}.png", GetTile)
            .WithName("GetMapTile")
            .WithTags("images")
            .Produces<byte[]>(StatusCodes.Status200OK, "image/png")
            .AllowAnonymous();
    }

    /// <summary>A whole facet as one PNG.</summary>
    /// <remarks>
    /// Open without a token. The entire facet at one pixel per map tile — 6144×4096 for Felucca — for
    /// downloading or printing rather than browsing; use the tiles for a viewer. Optional <c>?style=</c>
    /// picks <c>flat</c> (default) or <c>relief</c> as on the tile route. It is generated once and
    /// cached, and the first request is slow and memory-hungry if the staff pre-warm has not run.
    /// </remarks>
    private async Task<IResult> GetFull(string map, string? style, CancellationToken cancellationToken)
    {
        if (!TryParseFacet(map, out var facet))
        {
            return InvalidFacet(map);
        }

        if (!_maps.IsReady)
        {
            return NotLoaded();
        }

        if (!TryParseStyle(style, out var renderStyle))
        {
            return InvalidStyle(style!);
        }

        var path = await _maps.GetFullAsync(facet, renderStyle, cancellationToken);

        return path is null
            ? Results.Problem($"{facet} is not served by this shard.", statusCode: StatusCodes.Status404NotFound)
            : Results.File(path, "image/png");
    }

    /// <summary>One map tile.</summary>
    /// <remarks>
    /// Open without a token: map data is client data every player already has on disk. The facet is named,
    /// case-insensitively — felucca, trammel, ilshenar, malas, tokuno, termur. Zoom counts up to detail: 0
    /// is the whole facet in one tile, and maxZoom — which GET /api/v1/images/maps reports per facet — is
    /// one pixel per map tile. Tiles are 256×256. A tile at the facet's edge is padded transparent. Tiles
    /// are rendered on first request and cached, so the first call at a low zoom builds everything beneath
    /// it and is slow; the staff pre-warm exists to do that in advance. Optional <c>?style=</c> chooses the
    /// render: <c>flat</c> (default) is the radar map, <c>relief</c> adds altitude shading; each caches on
    /// its own.
    /// </remarks>
    private async Task<IResult> GetTile(
        string map,
        string z,
        int x,
        int y,
        string? style,
        CancellationToken cancellationToken
    )
    {
        if (!TryParseFacet(map, out var facet))
        {
            return InvalidFacet(map);
        }

        if (!_maps.IsReady)
        {
            return NotLoaded();
        }

        if (!int.TryParse(z, out var zoom))
        {
            // "full" reaching here means the caller wrote the whole-facet route wrong; the tile route
            // saying "not a zoom" would be true and useless.
            return Results.Problem(
                z.Equals("full", StringComparison.OrdinalIgnoreCase)
                    ? $"For the whole facet use /api/v1/images/maps/{map}/full.png."
                    : $"'{z}' is not a zoom. Expected a whole number from 0 to {_maps.MaxZoomFor(facet)}.",
                statusCode: StatusCodes.Status400BadRequest
            );
        }

        var maxZoom = _maps.MaxZoomFor(facet);

        if (zoom < 0 || zoom > maxZoom)
        {
            return Results.Problem(
                $"Zoom {zoom} is outside 0 to {maxZoom} for {facet}.",
                statusCode: StatusCodes.Status400BadRequest
            );
        }

        if (!TryParseStyle(style, out var renderStyle))
        {
            return InvalidStyle(style!);
        }

        var path = await _maps.GetTileAsync(facet, renderStyle, zoom, x, y, cancellationToken);

        return path is null
            ? Results.Problem(
                $"No tile {x},{y} at zoom {zoom} for {facet}.",
                statusCode: StatusCodes.Status404NotFound
            )
            : Results.File(path, "image/png");
    }

    private static IResult InvalidFacet(string name)
        => Results.Problem(
            $"'{name}' is not a map. Valid maps: {string.Join(", ", Enum.GetNames<MapType>())}.",
            statusCode: StatusCodes.Status400BadRequest
        );

    private static IResult InvalidStyle(string name)
        => Results.Problem(
            $"'{name}' is not a map style. Valid styles: {string.Join(", ", Enum.GetNames<MapRenderStyleType>())}.",
            statusCode: StatusCodes.Status400BadRequest
        );

    /// <summary>The facets this shard serves, and the shape of each one's tile grid.</summary>
    /// <remarks>
    /// Open without a token. Read this before requesting tiles: the facets' sizes come from the shard's own
    /// client files, so maxZoom and the grid differ between shards and must not be hardcoded.
    /// </remarks>
    private IResult List()
    {
        if (!_maps.IsReady)
        {
            return NotLoaded();
        }

        return Results.Ok(
            _provider.Facets
                .Select(facet => (Facet: facet, Map: _provider.Get(facet)))
                .Where(entry => entry.Map is not null)
                .Select(entry =>
                    {
                        var maxZoom = _maps.MaxZoomFor(entry.Facet);

                        return new MapFacetInfo(
                            entry.Facet.ToString(),
                            entry.Map!.Width,
                            entry.Map.Height,
                            maxZoom,
                            MapTileGeometry.TileSize,
                            MapTileGeometry.TilesAcross(entry.Map.Width, maxZoom, maxZoom),
                            MapTileGeometry.TilesDown(entry.Map.Height, maxZoom, maxZoom)
                        );
                    }
                )
        );
    }

    private static IResult NotLoaded()
        => Results.Problem(
            "The UO client files are not loaded; no maps can be served.",
            statusCode: StatusCodes.Status503ServiceUnavailable
        );

    /// <summary>
    /// Enum.TryParse accepts numeric strings, so "99" would parse into an undefined MapType and reach the
    /// provider as a facet nobody has. Enum.IsDefined is what stops it.
    /// </summary>
    private static bool TryParseFacet(string name, out MapType facet)
        => Enum.TryParse(name, true, out facet) && Enum.IsDefined(facet);

    /// <summary>
    /// The optional <c>?style=</c> query: absent or empty means the flat radar map. Case-insensitive.
    /// </summary>
    private static bool TryParseStyle(string? name, out MapRenderStyleType style)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            style = MapRenderStyleType.Flat;

            return true;
        }

        return Enum.TryParse(name, true, out style) && Enum.IsDefined(style);
    }
}
