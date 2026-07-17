using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Moongate.Http.Plugin.Interfaces.Endpoints;
using Moongate.Http.Plugin.Interfaces.Mobiles;

namespace Moongate.Http.Plugin.Endpoints.Mobiles;

/// <summary>Public PNG views over mobile-template paperdolls.</summary>
public sealed class PaperdollEndpoints : IApiEndpointRegistration
{
    private readonly IPaperdollImageService _paperdolls;

    public PaperdollEndpoints(IPaperdollImageService paperdolls)
    {
        _paperdolls = paperdolls;
    }

    public void Register(IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/v1/images/mobiles/templates/{id}/paperdoll.png", Get)
              .WithName("GetMobileTemplatePaperdoll")
              .WithTags("mobiles");
    }

    /// <summary>Serves the template's classic client paperdoll as PNG: background, body, hair, equipment.</summary>
    /// <remarks>
    /// Pass background=false to drop the backdrop gump. Hue and gender-random specs on the template
    /// resolve deterministically so the image is stable and cacheable. 404 on an unknown template id or
    /// when its body has no paperdoll gump.
    /// </remarks>
    private async Task<IResult> Get(string id, bool? background, CancellationToken cancellationToken)
    {
        var path = await _paperdolls.GetOrCreateAsync(id, background.GetValueOrDefault(true), cancellationToken);

        return path is null
            ? Results.Problem($"No paperdoll for template '{id}'.", statusCode: StatusCodes.Status404NotFound)
            : Results.File(path, "image/png");
    }
}
