using Moongate.Core.Geometry;
using Moongate.Server.Ultima.Entities.World;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     Lets NPC scripts sense the mobiles that come within <c>ultima.npcs.sense_range</c>:
///     <c>on_mobile_in_range(serial, other)</c>, once when the other mobile enters the range, as UOX3's onEnterRange and
///     POL's EnteredArea. Called on the game loop by the mobile service, after the sectors know the new position.
/// </summary>
public interface INpcSenseService
{
    /// <summary>
    ///     <paramref name="mobile" /> came into the world where it stands.
    /// </summary>
    void Appeared(MobileEntity mobile);

    /// <summary>
    ///     <paramref name="mobile" /> stepped from <paramref name="oldLocation" /> to where it stands, on the same map.
    /// </summary>
    void Moved(MobileEntity mobile, Point3D oldLocation);
}
