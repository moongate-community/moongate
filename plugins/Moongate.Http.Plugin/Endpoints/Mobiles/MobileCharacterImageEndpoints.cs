using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Net.Http.Headers;
using Moongate.Core.Primitives;
using Moongate.Http.Plugin.Data.Mobiles;
using Moongate.Http.Plugin.Interfaces.Endpoints;
using Moongate.Http.Plugin.Interfaces.Mobiles;

namespace Moongate.Http.Plugin.Endpoints.Mobiles;

/// <summary>Public PNG views of a real character, wearing what they actually wear.</summary>
public sealed class MobileCharacterImageEndpoints : IApiEndpointRegistration
{
    private readonly IMobileCharacterImageService _images;

    public MobileCharacterImageEndpoints(IMobileCharacterImageService images)
    {
        _images = images;
    }

    public void Register(IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/v1/images/mobiles/{serial:long}.png", GetFigure)
              .WithName("GetMobileCharacterImage")
              .WithTags("mobiles")
              .Produces<byte[]>(StatusCodes.Status200OK, "image/png");

        routes.MapGet("/api/v1/images/mobiles/{serial:long}/paperdoll.png", GetPaperdoll)
              .WithName("GetMobileCharacterPaperdoll")
              .WithTags("mobiles")
              .Produces<byte[]>(StatusCodes.Status200OK, "image/png");
    }

    /// <summary>Serves a character's dressed figure as PNG: body, hair and the items they are wearing.</summary>
    /// <remarks>
    /// The ETag is a fingerprint of the appearance, so a client that already has the current look gets
    /// 304 without a body. 404 when the serial names no mobile, or when its body has no animation.
    /// </remarks>
    private async Task<IResult> GetFigure(long serial, HttpRequest request, CancellationToken cancellationToken)
        => Respond(await _images.GetFigureAsync((Serial)(uint)serial, cancellationToken), request);

    /// <summary>Serves a character's paperdoll as PNG, with the equipment they are wearing.</summary>
    /// <remarks>
    /// Pass <c>background=false</c> for the doll without its backdrop. The ETag is a fingerprint of
    /// the appearance, so an unchanged character gets 304 without a body.
    /// </remarks>
    private async Task<IResult> GetPaperdoll(
        long serial,
        HttpRequest request,
        CancellationToken cancellationToken,
        bool background = true
    )
        => Respond(await _images.GetPaperdollAsync((Serial)(uint)serial, background, cancellationToken), request);

    /// <summary>
    /// The one HTTP decision this endpoint makes: hand back the file, or say the caller already has
    /// it. Comparing the fingerprint costs a string comparison where the alternative is a download.
    /// </summary>
    private static IResult Respond(MobileImage? image, HttpRequest request)
    {
        if (image is null)
        {
            return Results.Problem("No renderable image for that mobile.", statusCode: StatusCodes.Status404NotFound);
        }

        var tag = new EntityTagHeaderValue($"\"{image.Hash}\"");

        if (request.Headers.IfNoneMatch.Any(value => value == tag.ToString()))
        {
            return Results.StatusCode(StatusCodes.Status304NotModified);
        }

        return Results.File(image.Path, "image/png", entityTag: tag);
    }
}
