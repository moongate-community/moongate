using Moongate.Server.Ultima.Entities.World;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     Lets a mobile ride a creature: the creature turns into a worn item on the mount layer, that keeps what is needed
///     to make the creature again, and getting off makes it again beside the rider. The creature is not kept alive off
///     the map: an item worn by the rider is saved with it and nothing is left behind after a restart.
/// </summary>
/// <remarks>
///     Game loop only.
/// </remarks>
public interface IMountService
{
    /// <summary>
    ///     Gets whether <paramref name="rider" /> wears an item on the mount layer.
    /// </summary>
    bool IsMounted(MobileEntity rider);

    /// <summary>
    ///     Puts <paramref name="rider" /> on <paramref name="pet" />: the creature leaves the world and its mount item is
    ///     worn on the mount layer. Refused, with the reason told to the rider, when the rider is dead or already mounted,
    ///     when the creature is more than one tile away or when it is not the rider's own; false and silent when the
    ///     creature is no mount. <paramref name="force" /> skips the owner check, for the staff.
    /// </summary>
    bool TryMount(MobileEntity rider, MobileEntity pet, bool force = false);

    /// <summary>
    ///     Takes the mount item off <paramref name="rider" /> at once and makes the creature again on the rider's tile, with
    ///     its owner, off the game loop. False when the rider is not mounted.
    /// </summary>
    bool Dismount(MobileEntity rider);
}
