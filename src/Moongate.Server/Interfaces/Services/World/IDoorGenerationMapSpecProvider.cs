using Moongate.Server.Types.World;

namespace Moongate.Server.Interfaces.Services.World;

/// <summary>
/// Provides map/region specs used by door world generation.
/// </summary>
public interface IDoorGenerationMapSpecProvider
{
    /// <summary>
    /// Returns the map specs to scan.
    /// </summary>
    IReadOnlyList<DoorGenerationMapSpec> GetMapSpecs();
}
