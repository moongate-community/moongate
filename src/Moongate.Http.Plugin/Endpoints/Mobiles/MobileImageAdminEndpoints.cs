using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Moongate.Http.Plugin.Data;
using Moongate.Http.Plugin.Data.Api.Mobiles;
using Moongate.Http.Plugin.Data.Mobiles;
using Moongate.Http.Plugin.Interfaces.Endpoints;
using Moongate.Http.Plugin.Interfaces.Mobiles;
using Moongate.Http.Plugin.Services.Hosting;
using Moongate.Ultima.Types;
using System.Globalization;

namespace Moongate.Http.Plugin.Endpoints.Mobiles;

/// <summary>Staff pickers over bodies and hair styles, and the body image cache warm-up.</summary>
public sealed class MobileImageAdminEndpoints : IApiEndpointRegistration
{
    private readonly IAnimationCatalog _catalog;
    private readonly IBodyImageExportJob _export;
    private readonly IBodyImageService _images;

    public MobileImageAdminEndpoints(
        IAnimationCatalog catalog,
        IBodyImageExportJob export,
        IBodyImageService images
    )
    {
        _catalog = catalog;
        _export = export;
        _images = images;
    }

    public void Register(IEndpointRouteBuilder routes)
    {
        var export = routes.MapGroup("/api/v1/admin/images/bodies")
                           .WithTags("mobiles")
                           .RequireAuthorization(HttpServerService.AdminPolicy);

        export.MapPost("/", Start).WithName("StartBodyImageExport");
        export.MapGet("/", GetStatus).WithName("GetBodyImageExportStatus");

        routes.MapGet("/api/v1/admin/bodies", ListBodies)
              .WithName("ListBodies")
              .WithTags("mobiles")
              .RequireAuthorization(HttpServerService.AdminPolicy);

        routes.MapGet("/api/v1/admin/hair-styles", ListHairStyles)
              .WithName("ListHairStyles")
              .WithTags("mobiles")
              .RequireAuthorization(HttpServerService.AdminPolicy);
    }

    /// <summary>Generates every classified body's image into the cache.</summary>
    /// <remarks>
    /// Answers 202 and works in the background; poll this same route with GET for progress. Only one
    /// export runs at a time, so a second request while one is going answers 409. Unhued frames only —
    /// hued variants are generated on demand by the public image route.
    /// </remarks>
    private IResult Start()
    {
        if (!_images.IsReady)
        {
            return Results.Problem(
                "The UO client files are not loaded; there is nothing to export.",
                statusCode: StatusCodes.Status503ServiceUnavailable
            );
        }

        if (!_export.TryStart())
        {
            return Results.Problem(
                "A body image export is already running.",
                statusCode: StatusCodes.Status409Conflict
            );
        }

        return Results.Accepted("/api/v1/admin/images/bodies", _export.Status);
    }

    /// <summary>Reports how far the body image export has got.</summary>
    /// <remarks>State is Idle before any export has run, then Running, and finally Completed or Failed.</remarks>
    private IResult GetStatus()
        => Results.Ok(_export.Status);

    /// <summary>Every classified body, paged, for the picker.</summary>
    /// <remarks>
    /// Classification comes from mobtypes.txt; Equipment-type bodies never appear. Search matches the
    /// decimal id or the 0x-prefixed hex. Ordered by body id.
    /// </remarks>
    private IResult ListBodies(string? page, string? pageSize, string? search)
    {
        if (!PageRequest.TryParse(page, pageSize, search, out var request, out var error))
        {
            return Results.Problem(error, statusCode: StatusCodes.Status400BadRequest);
        }

        var classified = _catalog.ClassifiedBodies
                                 .Where(entry => entry.Type != MobType.Equipment)
                                 .Where(entry => Matches(entry.Body, request.Search))
                                 .OrderBy(entry => entry.Body)
                                 .ToArray();

        IReadOnlyList<BodySummary> items =
        [
            .. classified.Skip(request.Skip)
                         .Take(request.PageSize)
                         .Select(entry => BodySummary.From(entry.Body, entry.Type))
        ];

        return Results.Ok(PagedResponse<BodySummary>.From(items, classified.Length, request));
    }

    /// <summary>The selectable hair (or facial-hair) styles, paged.</summary>
    /// <remarks>Pass facial=true for beards. Search matches the style name, case-insensitively.</remarks>
    private IResult ListHairStyles(string? page, string? pageSize, string? search, bool? facial)
    {
        if (!PageRequest.TryParse(page, pageSize, search, out var request, out var error))
        {
            return Results.Problem(error, statusCode: StatusCodes.Status400BadRequest);
        }

        var source = facial.GetValueOrDefault() ? HairStyleCatalog.Facial : HairStyleCatalog.Hair;
        var matched = source
                      .Where(entry => request.Search is null
                                      || entry.Name.Contains(request.Search, StringComparison.OrdinalIgnoreCase))
                      .ToArray();

        IReadOnlyList<HairStyleSummary> items =
        [
            .. matched.Skip(request.Skip).Take(request.PageSize).Select(HairStyleSummary.From)
        ];

        return Results.Ok(PagedResponse<HairStyleSummary>.From(items, matched.Length, request));
    }

    private static bool Matches(int body, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return true;
        }

        var term = search.Trim();

        return body.ToString(CultureInfo.InvariantCulture).Contains(term, StringComparison.OrdinalIgnoreCase)
               || $"0x{body:X4}".Contains(term, StringComparison.OrdinalIgnoreCase);
    }
}
