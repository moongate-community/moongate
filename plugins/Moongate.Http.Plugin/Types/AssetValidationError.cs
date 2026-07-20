namespace Moongate.Http.Plugin.Types;

/// <summary>Why an asset upload was rejected (or <see cref="None" /> when accepted).</summary>
public enum AssetValidationError
{
    None,
    UnsupportedType,
    TooLarge
}
