using Moongate.Ultima.Types;

namespace Moongate.Ultima.Maps;

/// <summary>
/// Configuration for altitude-based shading effects
/// </summary>
public class AltitudeShadingSettings
{
    /// <summary>
    /// Surface normal Z-component (higher = softer shading)
    /// Sharp: 2.0, Normal: 4.0, Soft: 8.0+
    /// </summary>
    public float NormalZ { get; set; } = 8.0f;

    /// <summary>
    /// Brightness variation range (0.0 to 0.5)
    /// Sharp: 0.40 (±40%), Normal: 0.30 (±30%), Soft: 0.15 (±15%)
    /// </summary>
    public float BrightnessRange { get; set; } = 0.15f;

    /// <summary>
    /// Altitude gradient smoothing factor
    /// Sharp: 0.75, Normal: 0.50, Soft: 0.25
    /// </summary>
    public float GradientSmoothing { get; set; } = 0.25f;

    /// <summary>
    /// Gets preset configuration
    /// </summary>
    public static AltitudeShadingSettings GetPreset(AltitudeShadingPreset preset)
        => preset switch
        {
            AltitudeShadingPreset.Sharp => new()
            {
                NormalZ = 2.0f,
                BrightnessRange = 0.40f,
                GradientSmoothing = 0.75f
            },
            AltitudeShadingPreset.Normal => new()
            {
                NormalZ = 4.0f,
                BrightnessRange = 0.30f,
                GradientSmoothing = 0.50f
            },
            AltitudeShadingPreset.Soft => new()
            {
                NormalZ = 8.0f,
                BrightnessRange = 0.15f,
                GradientSmoothing = 0.25f
            },
            _ => new() // Default to Soft
        };
}
