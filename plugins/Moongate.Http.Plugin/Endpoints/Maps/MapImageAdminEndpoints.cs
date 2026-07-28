using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Moongate.Http.Plugin.Data;
using Moongate.Http.Plugin.Interfaces.Endpoints;
using Moongate.Http.Plugin.Interfaces.Maps;
using Moongate.Http.Plugin.Services.Hosting;

namespace Moongate.Http.Plugin.Endpoints.Maps;

/// <summary>Staff-only control of the map image cache.</summary>
public sealed class MapImageAdminEndpoints : IApiEndpointRegistration
{
    private readonly IMapImageExportJob _export;
    private readonly IMapImageService _maps;

    public MapImageAdminEndpoints(IMapImageExportJob export, IMapImageService maps)
    {
        _export = export;
        _maps = maps;
    }

    public void Register(IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/v1/admin/images/maps")
                          .WithTags("images")
                          .RequireAuthorization(HttpServerService.AdminPolicy);

        group.MapPost("/", Start)
             .WithName("StartMapImageExport")
             .Produces<MapImageExportStatus>(StatusCodes.Status202Accepted);
        group.MapGet("/", GetStatus).WithName("GetMapImageExportStatus").Produces<MapImageExportStatus>();
    }

    /// <summary>Reports how far the map image export has got.</summary>
    /// <remarks>
    /// State is Idle before any export has run, then Running, and finally Completed or Failed. Failed counts
    /// images that could not be produced; the reasons are in the server log.
    /// </remarks>
    private IResult GetStatus()
        => Results.Ok(_export.Status);

    /// <summary>Builds every map tile and whole-facet image into the cache.</summary>
    /// <remarks>
    /// Answers 202 and works in the background; poll this same route with GET for progress. Only one runs
    /// at a time, so a second request while one is going answers 409. Worth running before anyone opens a
    /// viewer: the first request at a low zoom otherwise builds every tile beneath it, and the first
    /// request for a whole facet renders a facet-sized image. Progress is kept in memory and does not
    /// survive a restart.
    /// </remarks>
    private IResult Start()
    {
        if (!_maps.IsReady)
        {
            return Results.Problem(
                "The UO client files are not loaded; there are no maps to export.",
                statusCode: StatusCodes.Status503ServiceUnavailable
            );
        }

        if (!_export.TryStart())
        {
            return Results.Problem(
                "A map image export is already running.",
                statusCode: StatusCodes.Status409Conflict
            );
        }

        return Results.Accepted("/api/v1/admin/images/maps", _export.Status);
    }
}
