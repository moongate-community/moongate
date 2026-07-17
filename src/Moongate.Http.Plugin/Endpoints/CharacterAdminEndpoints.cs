using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Moongate.Http.Plugin.Data;
using Moongate.Http.Plugin.Interfaces;
using Moongate.Http.Plugin.Services;
using Moongate.Http.Plugin.Data.Api;
using Moongate.Server.Abstractions.Interfaces.Accounts;

namespace Moongate.Http.Plugin.Endpoints;

/// <summary>Staff-only views over every character on the shard.</summary>
public sealed class CharacterAdminEndpoints : IApiEndpointRegistration
{
    private readonly ICharacterQueryService _characters;

    public CharacterAdminEndpoints(ICharacterQueryService characters)
    {
        _characters = characters;
    }

    public void Register(IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/v1/admin/characters", List)
              .WithName("ListCharacters")
              .WithTags("characters")
              .RequireAuthorization(HttpServerService.AdminPolicy);
    }

    /// <summary>Every player character on the shard, paged.</summary>
    /// <remarks>
    /// Ordered by character name. Pass search to filter: it is free text, case-insensitive, and matches
    /// either the character's name or the owning account's username, so "find me so-and-so's character" is
    /// one query. Page is 1-based and defaults to 1; pageSize defaults to 25 and cannot exceed 100. NPCs
    /// are not characters and never appear. A search matching nothing is an empty page, not an error.
    /// </remarks>
    private IResult List(string? page, string? pageSize, string? search)
    {
        if (!PageRequest.TryParse(page, pageSize, search, out var request, out var error))
        {
            return Results.Problem(error, statusCode: StatusCodes.Status400BadRequest);
        }

        var found = _characters.Search(request.Search, request.Skip, request.PageSize);

        IReadOnlyList<CharacterResponse> items =
            [.. found.Items.Select(owned => CharacterResponse.From(owned.Mobile, owned.AccountUsername))];

        return Results.Ok(PagedResponse<CharacterResponse>.From(items, found.Total, request));
    }
}
