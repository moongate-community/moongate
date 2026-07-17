using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Moongate.Http.Plugin.Interfaces.Endpoints;
using Moongate.Http.Plugin.Interfaces.Mobiles;

namespace Moongate.Http.Plugin.Endpoints.Mobiles;

/// <summary>Public PNG views over hair styles, rendered on a reference body.</summary>
public sealed class HairImageEndpoints : IApiEndpointRegistration
{
    private const int DefaultReferenceBody = 400;

    private readonly IHairImageService _hair;

    public HairImageEndpoints(IHairImageService hair)
    {
        _hair = hair;
    }

    public void Register(IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/v1/images/hair/{style}.png", Get)
              .WithName("GetHairImage")
              .WithTags("mobiles");
    }

    /// <summary>Serves a hair (or facial-hair) style as PNG, rendered over a reference body.</summary>
    /// <remarks>
    /// The style is the hair item's decimal graphic id (see /api/v1/admin/hair-styles). Pass hue to dye
    /// it, body to change the reference body (default 400, the human male), facial=true for beards.
    /// 404 when nothing decodes for the pair.
    /// </remarks>
    private async Task<IResult> Get(string style, int? hue, int? body, bool? facial, CancellationToken cancellationToken)
    {
        if (!int.TryParse(style, out var styleId) || styleId <= 0)
        {
            return Results.Problem("style must be a positive integer", statusCode: StatusCodes.Status400BadRequest);
        }

        var referenceBody = body is > 0 ? body.Value : DefaultReferenceBody;
        var path = await _hair.GetOrCreateAsync(
            styleId,
            hue.GetValueOrDefault(),
            referenceBody,
            facial.GetValueOrDefault(),
            cancellationToken
        );

        return path is null
            ? Results.Problem($"Nothing decodes for style {styleId}.", statusCode: StatusCodes.Status404NotFound)
            : Results.File(path, "image/png");
    }
}
