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
        routes.MapGet("/api/v1/images/mobiles/{serial}.png", GetFigure)
              .WithName("GetMobileCharacterImage")
              .WithTags("mobiles")
              .Produces<byte[]>(StatusCodes.Status200OK, "image/png");

        routes.MapGet("/api/v1/images/mobiles/{serial}/paperdoll.png", GetPaperdoll)
              .WithName("GetMobileCharacterPaperdoll")
              .WithTags("mobiles")
              .Produces<byte[]>(StatusCodes.Status200OK, "image/png");
    }

    /// <summary>Serves a character's dressed figure as PNG: body, hair and the items they are wearing.</summary>
    /// <remarks>
    /// The serial takes the form the rest of the API reports, <c>0x40000001</c>, or plain decimal.
    /// The ETag is a fingerprint of the appearance, so a client that already has the current look gets
    /// 304 without a body. 404 when the serial names no mobile, or when its body has no animation.
    /// </remarks>
    private async Task<IResult> GetFigure(string serial, HttpRequest request, CancellationToken cancellationToken)
    {
        if (!Serial.TryParse(serial, out var parsed))
        {
            return NotFound();
        }

        return Respond(await _images.GetFigureAsync(parsed, cancellationToken), request);
    }

    /// <summary>Serves a character's paperdoll as PNG, with the equipment they are wearing.</summary>
    /// <remarks>
    /// The serial takes the form the rest of the API reports, <c>0x40000001</c>, or plain decimal.
    /// Pass <c>background=false</c> for the doll without its backdrop. The ETag is a fingerprint of
    /// the appearance, so an unchanged character gets 304 without a body.
    /// </remarks>
    private async Task<IResult> GetPaperdoll(
        string serial,
        HttpRequest request,
        CancellationToken cancellationToken,
        bool background = true
    )
    {
        if (!Serial.TryParse(serial, out var parsed))
        {
            return NotFound();
        }

        return Respond(await _images.GetPaperdollAsync(parsed, background, cancellationToken), request);
    }

    /// <summary>
    /// The one HTTP decision this endpoint makes: hand back the file, or say the caller already has
    /// it. Comparing the fingerprint costs a string comparison where the alternative is a download.
    /// </summary>
    private static IResult Respond(MobileImage? image, HttpRequest request)
    {
        if (image is null)
        {
            return NotFound();
        }

        var tag = new EntityTagHeaderValue($"\"{image.Hash}\"");

        if (request.Headers.IfNoneMatch.Any(value => value == tag.ToString()))
        {
            return Results.StatusCode(StatusCodes.Status304NotModified);
        }

        return Results.File(image.Path, "image/png", entityTag: tag);
    }

    /// <summary>
    /// One answer for "that is not a character" and "that is not even a serial": from outside, both
    /// mean the picture asked for does not exist.
    /// </summary>
    private static IResult NotFound()
        => Results.Problem("No renderable image for that mobile.", statusCode: StatusCodes.Status404NotFound);
}
