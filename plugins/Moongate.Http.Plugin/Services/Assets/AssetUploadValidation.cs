using Moongate.Http.Plugin.Data.Assets;
using Moongate.Http.Plugin.Types;

namespace Moongate.Http.Plugin.Services.Assets;

/// <summary>Validates an asset upload's declared content-type and size. Pure — no I/O.</summary>
public static class AssetUploadValidation
{
    /// <summary>The accepted image content-types mapped to the on-disk extension used for each.</summary>
    public static IReadOnlyDictionary<string, string> AllowedContentTypes { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["image/png"] = "png",
            ["image/jpeg"] = "jpg",
            ["image/webp"] = "webp",
            ["image/svg+xml"] = "svg",
            ["image/x-icon"] = "ico"
        };

    public static AssetValidationResult Validate(string contentType, long length, long maxBytes)
    {
        if (!AllowedContentTypes.TryGetValue(contentType, out var extension))
        {
            return new() { Ok = false, Error = AssetValidationError.UnsupportedType };
        }

        if (length > maxBytes)
        {
            return new() { Ok = false, Error = AssetValidationError.TooLarge };
        }

        return new() { Ok = true, Extension = extension };
    }
}
