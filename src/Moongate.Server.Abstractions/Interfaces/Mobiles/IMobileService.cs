using Moongate.Core.Primitives;

namespace Moongate.Server.Abstractions.Interfaces.Mobiles;

/// <summary>
/// Owns mobile state that scripts and systems mutate outside the movement rules. It is the seam
/// <c>MobileModule</c> lacked: the module wrote the entity store directly and so could carry no
/// loop-affinity guard of its own.
/// </summary>
public interface IMobileService
{
    /// <summary>
    /// Places the mobile at (x, y, z) on its current map <b>without validating the terrain</b> — a GM
    /// move or a spawner placement, not a walk. <see cref="World.IMovementService" /> owns the
    /// validated verb. False on an unknown serial.
    /// </summary>
    bool Teleport(Serial mobile, int x, int y, int z);
}
