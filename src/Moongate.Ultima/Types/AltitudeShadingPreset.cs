namespace Moongate.Ultima.Types;

/// <summary>
/// Altitude shading preset configuration
/// </summary>
public enum AltitudeShadingPreset
{
    /// <summary>
    /// Dramatic, high-contrast shading with sharp edges
    /// </summary>
    Sharp,
    /// <summary>
    /// More pronounced shading with higher contrast
    /// </summary>
    Normal,
    /// <summary>
    /// Very subtle, smooth shading (matches UO client closely)
    /// </summary>
    Soft,
    /// <summary>
    /// Custom settings (uses manual configuration)
    /// </summary>
    Custom
}
