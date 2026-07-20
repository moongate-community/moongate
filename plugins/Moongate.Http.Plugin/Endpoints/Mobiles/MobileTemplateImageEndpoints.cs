using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Moongate.Http.Plugin.Interfaces.Endpoints;
using Moongate.Http.Plugin.Interfaces.Mobiles;

namespace Moongate.Http.Plugin.Endpoints.Mobiles;

/// <summary>Public PNG views over dressed mobile-template figures.</summary>
public sealed class MobileTemplateImageEndpoints : IApiEndpointRegistration
{
    private readonly IMobileTemplateImageService _figures;

    public MobileTemplateImageEndpoints(IMobileTemplateImageService figures)
    {
        _figures = figures;
    }

    public void Register(IEndpointRouteBuilder routes)
        => routes.MapGet("/api/v1/images/mobiles/templates/{id}.png", Get)
                 .WithName("GetMobileTemplateImage")
                 .WithTags("mobiles");

    /// <summary>Serves a mobile template's dressed figure as PNG: body, hair and worn equipment.</summary>
    /// <remarks>
    /// Hue specs on the template resolve to their low end so the image is stable and cacheable. 404 on
    /// an unknown template id, or when the template's body has no animation.
    /// </remarks>
    private async Task<IResult> Get(string id, CancellationToken cancellationToken)
    {
        var path = await _figures.GetOrCreateAsync(id, cancellationToken);

        return path is null
                   ? Results.Problem($"No renderable figure for template '{id}'.", statusCode: StatusCodes.Status404NotFound)
                   : Results.File(path, "image/png");
    }
}
