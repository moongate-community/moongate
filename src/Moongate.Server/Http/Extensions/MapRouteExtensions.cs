using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Moongate.Core.Types;
using Moongate.Server.Http.Internal;
using SixLabors.ImageSharp.Formats.Png;

namespace Moongate.Server.Http.Extensions;

internal static class MapRouteExtensions
{
    public static IEndpointRouteBuilder MapMapRoutes(
        this IEndpointRouteBuilder endpoints,
        MoongateHttpRouteContext context
    )
    {
        if (context.MapImageService is null)
        {
            return endpoints;
        }

        var mapsGroup = endpoints.MapGroup("/api/maps").WithTags("Maps");

        if (context.JwtOptions.IsEnabled)
        {
            mapsGroup.RequireAuthorization();
        }

        mapsGroup.MapGet(
                     "/{mapId}.png",
                     (int mapId, CancellationToken cancellationToken) =>
                         HandleGetMapImage(context, mapId, cancellationToken)
                 )
                 .WithName("MapsGetImage")
                 .WithSummary("Returns a radar-color PNG image of the specified map.")
                 .Produces(StatusCodes.Status200OK, contentType: "image/png")
                 .Produces(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static IResult HandleGetMapImage(
        MoongateHttpRouteContext context,
        int mapId,
        CancellationToken cancellationToken
    )
    {
        _ = cancellationToken;

        var mapsImageDirectory = Path.Combine(context.DirectoriesConfig[DirectoryType.Images], "maps");
        Directory.CreateDirectory(mapsImageDirectory);

        var cachePath = Path.Combine(mapsImageDirectory, $"{mapId}.png");

        if (File.Exists(cachePath))
        {
            return Results.File(cachePath, "image/png");
        }

        using var image = context.MapImageService!.GetMapImage(mapId);

        if (image is null)
        {
            return TypedResults.NotFound();
        }

        using (var stream = File.Create(cachePath))
        {
            image.Save(stream, new PngEncoder());
        }

        return Results.File(cachePath, "image/png");
    }
}
