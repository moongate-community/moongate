using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Moongate.Http.Plugin.Interfaces.Endpoints;
using Moongate.Http.Plugin.Interfaces.Images;
using Moongate.Http.Plugin.Services.Hosting;

namespace Moongate.Http.Plugin.Endpoints.Images;

/// <summary>Staff-only control of the item image cache.</summary>
public sealed class ItemImageAdminEndpoints : IApiEndpointRegistration
{
    private readonly IItemImageExportJob _export;
    private readonly IItemImageService _images;

    public ItemImageAdminEndpoints(IItemImageExportJob export, IItemImageService images)
    {
        _export = export;
        _images = images;
    }

    public void Register(IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/v1/admin/images/items")
                          .WithTags("images")
                          .RequireAuthorization(HttpServerService.AdminPolicy);

        group.MapPost("/", Start).WithName("StartItemImageExport");
        group.MapGet("/", GetStatus).WithName("GetItemImageExportStatus");
    }

    /// <summary>Reports how far the item image export has got.</summary>
    /// <remarks>
    /// State is Idle before any export has run, then Running, and finally Completed or Failed. Failed
    /// counts items whose art could not be written; the reasons are in the server log.
    /// </remarks>
    private IResult GetStatus()
        => Results.Ok(_export.Status);

    /// <summary>Generates every item's art into the cache.</summary>
    /// <remarks>
    /// Answers 202 and works in the background; poll this same route with GET for progress. Only one
    /// export runs at a time, so a second request while one is going answers 409. Unhued art only — hued
    /// variants are generated on demand by the public image route. Progress is kept in memory and does
    /// not survive a restart.
    /// </remarks>
    private IResult Start()
    {
        if (!_images.IsReady)
        {
            return Results.Problem(
                "The UO client files are not loaded; there is no art to export.",
                statusCode: StatusCodes.Status503ServiceUnavailable
            );
        }

        if (!_export.TryStart())
        {
            return Results.Problem(
                "An item image export is already running.",
                statusCode: StatusCodes.Status409Conflict
            );
        }

        return Results.Accepted("/api/v1/admin/images/items", _export.Status);
    }
}
