using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Moongate.Http.Plugin.Interfaces.Endpoints;
using Moongate.Http.Plugin.Interfaces.Mobiles;

namespace Moongate.Http.Plugin.Endpoints.Mobiles;

/// <summary>Public PNG views over UO body graphics.</summary>
public sealed class BodyImageEndpoints : IApiEndpointRegistration
{
    private readonly IBodyImageService _bodies;

    public BodyImageEndpoints(IBodyImageService bodies)
    {
        _bodies = bodies;
    }

    public void Register(IEndpointRouteBuilder routes)
        => routes.MapGet("/api/v1/images/bodies/{body}.png", Get)
                 .WithName("GetBodyImage")
                 .WithTags("mobiles")
                 .Produces<byte[]>(StatusCodes.Status200OK, "image/png");

    /// <summary>Serves a body's idle, front-facing frame as PNG.</summary>
    /// <remarks>
    /// The body is a decimal graphic id; pass hue for a skin-hued variant. Generated lazily and cached
    /// on disk. A body with no usable animation answers 404. Anonymous on purpose: it is client data
    /// every player already has on disk.
    /// </remarks>
    private async Task<IResult> Get(string body, int? hue, CancellationToken cancellationToken)
    {
        if (!int.TryParse(body, out var bodyId) || bodyId < 0)
        {
            return Results.Problem("body must be a non-negative integer", statusCode: StatusCodes.Status400BadRequest);
        }

        var path = await _bodies.GetOrCreateAsync(bodyId, hue.GetValueOrDefault(), cancellationToken);

        return path is null
                   ? Results.Problem($"No animation for body {bodyId}.", statusCode: StatusCodes.Status404NotFound)
                   : Results.File(path, "image/png");
    }
}
