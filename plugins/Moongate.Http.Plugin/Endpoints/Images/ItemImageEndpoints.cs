using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Moongate.Http.Plugin.Interfaces.Endpoints;
using Moongate.Http.Plugin.Interfaces.Images;

namespace Moongate.Http.Plugin.Endpoints.Images;

/// <summary>Item art as PNG, straight from the UO client files.</summary>
public sealed class ItemImageEndpoints : IApiEndpointRegistration
{
    /// <summary>Hues.GetHue indexes 0..2999 once the client's 1-based hue is undone; 0 means unhued.</summary>
    private const uint MaxHue = 3000;

    private readonly IItemImageService _images;

    public ItemImageEndpoints(IItemImageService images)
    {
        _images = images;
    }

    public void Register(IEndpointRouteBuilder routes)

        // A method group, not a lambda: Swashbuckle reads the /// off the handler's method, and a lambda
        // has none — the route would document itself blank.
        => routes.MapGet("/api/v1/images/items/{id}.png", Get)
                 .WithName("GetItemImage")
                 .WithTags("images")
                 .Produces<byte[]>(StatusCodes.Status200OK, "image/png")
                 .AllowAnonymous();

    /// <summary>
    /// Hex, with or without the prefix. A bare "1234" is hex too: the route is documented as 0xITEM_ID,
    /// and reading it as decimal would quietly serve 0x4D2 to someone who meant 0x1234.
    /// </summary>
    internal static bool TryParseHex(string? value, out uint parsed)
    {
        parsed = 0;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var digits = value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? value[2..] : value;

        return uint.TryParse(digits, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out parsed);
    }

    /// <summary>
    /// An absent hue is unhued. The range is checked here because Hues.GetHue never fails — it masks the
    /// index and falls back to hue 0 — so an out-of-range value would serve a wrong image, not an error.
    /// </summary>
    internal static bool TryParseHue(string? value, out ushort hue)
    {
        hue = 0;

        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (!TryParseHex(value, out var parsed) || parsed > MaxHue)
        {
            return false;
        }

        hue = (ushort)parsed;

        return true;
    }

    /// <summary>Item art as a PNG, optionally hued.</summary>
    /// <remarks>
    /// Open without a token: the art is client data every player already has on disk. The id is hex, with
    /// or without the 0x prefix — 0x1234.png and 1234.png are the same item. Pass hue for a coloured
    /// variant; 0, or omitting it, gives the raw art, and the range is 0 to 3000. Images are decoded on
    /// first request and cached, so the first call for an item is slower than every call after it.
    /// </remarks>
    private async Task<IResult> Get(string id, string? hue, CancellationToken cancellationToken)
    {
        if (!TryParseHex(id, out var itemId))
        {
            return Results.Problem(
                $"'{id}' is not an item id. Expected hex, such as 0x1234.",
                statusCode: StatusCodes.Status400BadRequest
            );
        }

        if (!TryParseHue(hue, out var hueValue))
        {
            return Results.Problem(
                $"'{hue}' is not a hue. Expected hex from 0 to {MaxHue}, where 0 is unhued.",
                statusCode: StatusCodes.Status400BadRequest
            );
        }

        // Checked before the lookup, because a missing image and missing client files are indistinguishable
        // afterwards: a shard with a wrong UltimaDirectory would answer 404, telling the operator this item
        // does not exist when the truth is that none of them do.
        if (!_images.IsReady)
        {
            return Results.Problem(
                "The UO client files are not loaded; no item art can be served.",
                statusCode: StatusCodes.Status503ServiceUnavailable
            );
        }

        var path = await _images.GetOrCreateAsync(itemId, hueValue, cancellationToken);

        return path is null
                   ? Results.Problem(
                       $"No art for item 0x{itemId:x4}.",
                       statusCode: StatusCodes.Status404NotFound
                   )
                   : Results.File(path, "image/png");
    }
}
