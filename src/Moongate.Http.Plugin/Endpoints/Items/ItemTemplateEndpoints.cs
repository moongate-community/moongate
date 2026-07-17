using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Moongate.Http.Plugin.Data;
using Moongate.Http.Plugin.Data.Api.Items;
using Moongate.Http.Plugin.Interfaces.Endpoints;
using Moongate.Http.Plugin.Services.Hosting;
using Moongate.Server.Abstractions.Interfaces.Items;
using Moongate.UO.Data.Items;

namespace Moongate.Http.Plugin.Endpoints.Items;

/// <summary>Staff-only, read-only views over the item template registry.</summary>
public sealed class ItemTemplateEndpoints : IApiEndpointRegistration
{
    private readonly IItemTemplateService _templates;

    public ItemTemplateEndpoints(IItemTemplateService templates)
    {
        _templates = templates;
    }

    public void Register(IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/v1/admin/items/templates", List)
              .WithName("ListItemTemplates")
              .WithTags("items")
              .RequireAuthorization(HttpServerService.AdminPolicy);

        routes.MapGet("/api/v1/admin/items/templates/{id}", Get)
              .WithName("GetItemTemplate")
              .WithTags("items")
              .RequireAuthorization(HttpServerService.AdminPolicy);
    }

    /// <summary>Every item template, paged.</summary>
    /// <remarks>
    /// Ordered by template id. Pass search to filter: free text, case-insensitive, matching the
    /// template's id, name, category or any tag. Page is 1-based and defaults to 1; pageSize defaults
    /// to 25 and cannot exceed 100. A search matching nothing is an empty page, not an error.
    /// </remarks>
    private IResult List(string? page, string? pageSize, string? search)
    {
        if (!PageRequest.TryParse(page, pageSize, search, out var request, out var error))
        {
            return Results.Problem(error, statusCode: StatusCodes.Status400BadRequest);
        }

        var matched = Filter(_templates.All, request.Search);

        IReadOnlyList<ItemTemplateSummaryResponse> items =
        [
            .. matched.OrderBy(template => template.Id, StringComparer.OrdinalIgnoreCase)
                      .Skip(request.Skip)
                      .Take(request.PageSize)
                      .Select(ItemTemplateSummaryResponse.From)
        ];

        return Results.Ok(PagedResponse<ItemTemplateSummaryResponse>.From(items, matched.Count, request));
    }

    /// <summary>Fetches one item template by id, specs included.</summary>
    /// <remarks>Ids are case-insensitive. Answers 404 when no template carries the id.</remarks>
    private IResult Get(string id)
    {
        var template = _templates.GetById(id);

        return template is null
            ? Results.Problem($"No item template with id '{id}'.", statusCode: StatusCodes.Status404NotFound)
            : Results.Ok(ItemTemplateResponse.From(template));
    }

    private static List<ItemTemplate> Filter(IReadOnlyList<ItemTemplate> all, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return [.. all];
        }

        return
        [
            .. all.Where(template =>
                Matches(template.Id, search)
                || Matches(template.Name, search)
                || Matches(template.Category, search)
                || template.Tags.Any(tag => Matches(tag, search)))
        ];
    }

    private static bool Matches(string value, string search)
        => value.Contains(search, StringComparison.OrdinalIgnoreCase);
}
