using Moongate.Abstractions.Interfaces.Services.Base;
using Moongate.UO.Data.Geometry;

namespace Moongate.Server.Interfaces.Services.Spatial;

/// <summary>
/// Provides global/personal light cycle management and override controls.
/// </summary>
public interface ILightService : IMoongateService
{
    /// <summary>
    /// Computes global light level using day/night cycle rules inspired by ModernUO LightCycle.
    /// </summary>
    /// <param name="utcNow">Optional UTC timestamp. Uses current UTC time when omitted.</param>
    /// <returns>Global light level byte (0 = day, higher = darker).</returns>
    int ComputeGlobalLightLevel(DateTime? utcNow = null);

    /// <summary>
    /// Computes global light level for the provided map/location using ModernUO Clock/GetTime style offsets.
    /// Dungeon and jail regions can override the global level.
    /// </summary>
    /// <param name="mapId">Map id.</param>
    /// <param name="location">World location.</param>
    /// <param name="utcNow">Optional UTC timestamp. Uses current UTC time when omitted.</param>
    /// <returns>Global light level byte (0 = day, higher = darker).</returns>
    int ComputeGlobalLightLevel(int mapId, Point3D location, DateTime? utcNow = null);

    /// <summary>
    /// Sets or clears a global light override for all connected players.
    /// </summary>
    /// <param name="lightLevel">Forced light level (0-255), or <c>null</c> to clear override.</param>
    /// <param name="applyImmediately">When true, sends light update packets immediately.</param>
    void SetGlobalLightOverride(int? lightLevel, bool applyImmediately = true);
}
