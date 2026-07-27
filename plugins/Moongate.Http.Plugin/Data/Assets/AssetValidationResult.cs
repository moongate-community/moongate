using Moongate.Http.Plugin.Types;

namespace Moongate.Http.Plugin.Data.Assets;

/// <summary>The result of validating an asset upload: on success carries the file extension to use.</summary>
public sealed class AssetValidationResult
{
    public bool Ok { get; init; }

    public string? Extension { get; init; }

    public AssetValidationError Error { get; init; }
}
