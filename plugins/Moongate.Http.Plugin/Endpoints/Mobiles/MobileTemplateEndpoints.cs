using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Moongate.Http.Plugin.Data;
using Moongate.Http.Plugin.Data.Api.Mobiles;
using Moongate.Http.Plugin.Interfaces.Endpoints;
using Moongate.Http.Plugin.Services.Hosting;
using Moongate.Server.Abstractions.Interfaces.Mobiles;
using Moongate.UO.Data.Mobiles.Templates;

namespace Moongate.Http.Plugin.Endpoints.Mobiles;

/// <summary>Staff-only, read-only views over the mobile spawn template registry.</summary>
public sealed class MobileTemplateEndpoints : IApiEndpointRegistration
{
    private readonly IMobileTemplateService _templates;

    public MobileTemplateEndpoints(IMobileTemplateService templates)
    {
        _templates = templates;
    }

    public void Register(IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/v1/admin/mobiles/templates", List)
              .WithName("ListMobileTemplates")
              .WithTags("mobiles")
              .Produces<PagedResponse<MobileTemplateSummaryResponse>>()
              .RequireAuthorization(HttpServerService.AdminPolicy);

        routes.MapGet("/api/v1/admin/mobiles/templates/{id}", Get)
              .WithName("GetMobileTemplate")
              .WithTags("mobiles")
              .Produces<MobileTemplateResponse>()
              .RequireAuthorization(HttpServerService.AdminPolicy);
    }

    /// <summary>Fetches one mobile template by id, variants and equipment included.</summary>
    /// <remarks>Ids are case-insensitive. Answers 404 when no template carries the id.</remarks>
    private IResult Get(string id)
    {
        var template = _templates.GetById(id);

        return template is null
                   ? Results.Problem($"No mobile template with id '{id}'.", statusCode: StatusCodes.Status404NotFound)
                   : Results.Ok(MobileTemplateResponse.From(template));
    }

    /// <summary>Every mobile template, paged.</summary>
    /// <remarks>
    /// Ordered by template id. Pass search to filter: free text, case-insensitive, matching the
    /// template's id, name, title, category or any tag. Page is 1-based and defaults to 1; pageSize
    /// defaults to 25 and cannot exceed 100. A search matching nothing is an empty page, not an error.
    ///
    /// A template's hues are reported as the specs they are — a value or a range resolved per spawn —
    /// rather than as numbers.
    /// </remarks>
    private IResult List(string? page, string? pageSize, string? search)
    {
        if (!PageRequest.TryParse(page, pageSize, search, out var request, out var error))
        {
            return Results.Problem(error, statusCode: StatusCodes.Status400BadRequest);
        }

        var matched = Filter(_templates.All, request.Search);

        IReadOnlyList<MobileTemplateSummaryResponse> items =
        [
            .. matched.Skip(request.Skip).Take(request.PageSize).Select(MobileTemplateSummaryResponse.From)
        ];

        return Results.Ok(PagedResponse<MobileTemplateSummaryResponse>.From(items, matched.Count, request));
    }

    /// <summary>
    /// The registry already hands templates back ordered by id, so this only narrows them — paging a
    /// re-sorted list would be a second ordering to keep in step with the first.
    /// </summary>
    private static List<MobileTemplate> Filter(IReadOnlyList<MobileTemplate> all, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return [.. all];
        }

        return
        [
            .. all.Where(
                template =>
                    Matches(template.Id, search) ||
                    Matches(template.Name, search) ||
                    Matches(template.Title, search) ||
                    Matches(template.Category, search) ||
                    template.Tags.Any(tag => Matches(tag, search))
            )
        ];
    }

    private static bool Matches(string value, string search)
        => value.Contains(search, StringComparison.OrdinalIgnoreCase);
}
