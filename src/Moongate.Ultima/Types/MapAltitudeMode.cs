namespace Moongate.Ultima.Types;

/// <summary>
/// Altitude rendering mode for map preview generation
/// </summary>
public enum MapAltitudeMode
{
    /// <summary>
    /// Normal flat rendering without altitude effects
    /// </summary>
    Normal,
    /// <summary>
    /// Normal rendering with altitude-based shading
    /// </summary>
    NormalWithAltitude,
    /// <summary>
    /// Pure altitude map (grayscale based on height)
    /// </summary>
    Altitude
}
